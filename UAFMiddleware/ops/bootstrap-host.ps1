param(
    [string]$MiddlewareServiceName = 'UAFSageMiddleware',
    [string]$CloudflaredPrimaryName = 'Cloudflared',
    [string]$CloudflaredSecondaryName = 'cloudflared',
    [string]$CloudflaredConfigPath = 'C:\UAF-Auto\.cloudflared\config.yml',
    [string]$StartupTaskName = 'UAF-VerifyServices',
    [switch]$AllowMissingCloudflared,
    [switch]$SkipTunnelCheck,
    [switch]$SkipVerification
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'lib/common.ps1')

$logFile = New-LogFilePath -Name 'bootstrap-host.log'
$originalArgs = @($MyInvocation.UnboundArguments)
Ensure-Admin -ScriptPath $PSCommandPath -Arguments $originalArgs

Write-OpsLog -Message 'Starting host bootstrap' -LogFile $logFile

$middlewareService = Get-ServiceInfoByNames -Names @($MiddlewareServiceName)
if (-not $middlewareService.Exists) {
    Write-OpsLog -Message "Middleware service '$MiddlewareServiceName' is missing. Install it first with install-service.bat." -Level 'ERROR' -LogFile $logFile
    exit 1
}

Set-Service -Name $middlewareService.Name -StartupType Automatic
Write-OpsLog -Message "Middleware service '$($middlewareService.Name)' set to Automatic startup" -LogFile $logFile

# Ensure recovery actions are configured.
sc.exe failure $middlewareService.Name reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
Write-OpsLog -Message "Middleware service '$($middlewareService.Name)' recovery actions configured" -LogFile $logFile

$cloudflaredService = Get-ServiceInfoByNames -Names @($CloudflaredPrimaryName, $CloudflaredSecondaryName)
if (-not $cloudflaredService.Exists) {
    $cloudflaredCmd = Get-Command cloudflared -ErrorAction SilentlyContinue

    if ($null -eq $cloudflaredCmd) {
        if ($AllowMissingCloudflared) {
            Write-OpsLog -Message 'Cloudflared binary not found; skipping service setup because AllowMissingCloudflared was set' -Level 'WARN' -LogFile $logFile
        }
        else {
            Write-OpsLog -Message 'Cloudflared binary not found. Install cloudflared and rerun bootstrap.' -Level 'ERROR' -LogFile $logFile
            exit 1
        }
    }
    elseif (-not (Test-Path -LiteralPath $CloudflaredConfigPath)) {
        if ($AllowMissingCloudflared) {
            Write-OpsLog -Message "Cloudflared config '$CloudflaredConfigPath' not found; skipping service setup because AllowMissingCloudflared was set" -Level 'WARN' -LogFile $logFile
        }
        else {
            Write-OpsLog -Message "Cloudflared config '$CloudflaredConfigPath' not found" -Level 'ERROR' -LogFile $logFile
            exit 1
        }
    }
    else {
        Write-OpsLog -Message "Installing cloudflared service with config '$CloudflaredConfigPath'" -LogFile $logFile
        & $cloudflaredCmd.Source service install --config $CloudflaredConfigPath | Out-Null
        Start-Sleep -Seconds 2
    }

    $cloudflaredService = Get-ServiceInfoByNames -Names @($CloudflaredPrimaryName, $CloudflaredSecondaryName)
}

if ($cloudflaredService.Exists) {
    Set-Service -Name $cloudflaredService.Name -StartupType Automatic
    Write-OpsLog -Message "Cloudflared service '$($cloudflaredService.Name)' set to Automatic startup" -LogFile $logFile
}
elseif (-not $AllowMissingCloudflared) {
    Write-OpsLog -Message 'Cloudflared service is not installed' -Level 'ERROR' -LogFile $logFile
    exit 1
}

$verifyScriptPath = Join-Path (Split-Path $scriptRoot -Parent) 'verify-services.ps1'
if (-not (Test-Path -LiteralPath $verifyScriptPath)) {
    Write-OpsLog -Message "Startup verification script missing: $verifyScriptPath" -Level 'ERROR' -LogFile $logFile
    exit 1
}

Ensure-ScheduledTaskStartupVerifier -TaskName $StartupTaskName -ScriptPath $verifyScriptPath -LogFile $logFile

if (-not (Ensure-ServiceRunning -Name $middlewareService.Name -TimeoutSeconds 30)) {
    Write-OpsLog -Message "Failed to start middleware service '$($middlewareService.Name)'" -Level 'ERROR' -LogFile $logFile
    exit 1
}

if ($cloudflaredService.Exists) {
    if (-not (Ensure-ServiceRunning -Name $cloudflaredService.Name -TimeoutSeconds 30)) {
        Write-OpsLog -Message "Failed to start cloudflared service '$($cloudflaredService.Name)'" -Level 'ERROR' -LogFile $logFile
        exit 1
    }
}

if (-not $SkipVerification) {
    $verifyScript = Join-Path $scriptRoot 'verify-stack.ps1'
    $verifyArgs = @('-Repair')
    if ($SkipTunnelCheck) {
        $verifyArgs += '-SkipTunnelCheck'
    }

    if ($AllowMissingCloudflared) {
        $verifyArgs += '-SkipTunnelCheck'
        $verifyArgs += '-AllowMissingCloudflared'
    }

    Write-OpsLog -Message 'Running post-bootstrap verification' -LogFile $logFile
    & $verifyScript @verifyArgs
    if ($LASTEXITCODE -ne 0) {
        Write-OpsLog -Message "Post-bootstrap verification failed with exit code $LASTEXITCODE" -Level 'ERROR' -LogFile $logFile
        exit $LASTEXITCODE
    }
}

Write-OpsLog -Message 'Host bootstrap completed successfully' -LogFile $logFile
exit 0
