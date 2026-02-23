param(
    [string]$MiddlewareServiceName = 'UAFSageMiddleware',
    [string]$CloudflaredPrimaryName = 'Cloudflared',
    [string]$CloudflaredSecondaryName = 'cloudflared',
    [string]$ProjectRelativePath = 'src\UAFMiddleware.csproj',
    [string]$PublishRuntime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$PullLatest,
    [switch]$SkipTunnelCheck,
    [int]$HealthRetries = 18,
    [int]$HealthDelaySeconds = 5,
    [string]$LocalLivenessUrl = 'http://localhost:3000/health',
    [string]$LocalHealthUrl = 'http://localhost:3000/health/ready',
    [string]$TunnelHealthUrl = 'https://sage.uaf-automation.uk/health/ready'
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path $scriptRoot -Parent
. (Join-Path $scriptRoot 'lib/common.ps1')

$logFile = New-LogFilePath -Name 'update-middleware.log'
$originalArgs = @($MyInvocation.UnboundArguments)
Ensure-Admin -ScriptPath $PSCommandPath -Arguments $originalArgs

Write-OpsLog -Message 'Starting middleware update workflow' -LogFile $logFile

function Resolve-CloudflaredServiceName {
    param(
        [string]$PrimaryName,
        [string]$SecondaryName
    )

    $cloudflaredService = Get-ServiceInfoByNames -Names @($PrimaryName, $SecondaryName)
    if ($cloudflaredService.Exists) {
        return $cloudflaredService.Name
    }

    return $null
}

$serviceInfo = Get-ServiceInfoByNames -Names @($MiddlewareServiceName)
if (-not $serviceInfo.Exists) {
    Write-OpsLog -Message "Service '$MiddlewareServiceName' is not installed" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$serviceExe = Get-ServiceExecutablePath -ServiceName $serviceInfo.Name
if ([string]::IsNullOrWhiteSpace($serviceExe)) {
    Write-OpsLog -Message "Unable to resolve service binary path for '$($serviceInfo.Name)'" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$installDir = Split-Path -Parent $serviceExe
if (-not (Test-Path -LiteralPath $installDir)) {
    Write-OpsLog -Message "Install directory not found: $installDir" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$projectPath = Join-Path $repoRoot $ProjectRelativePath
if (-not (Test-Path -LiteralPath $projectPath)) {
    Write-OpsLog -Message "Project file not found: $projectPath" -Level 'ERROR' -LogFile $logFile
    exit 1
}

$publishDir = Join-Path $repoRoot 'publish'
$effectivePublishDir = $publishDir
$backupRoot = Join-Path $installDir 'backups'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $backupRoot $timestamp
$rollbackOnFailure = $true
$touchedInstall = $false
$localLivenessPassed = $false
$localHealthPassed = $false
$tunnelHealthPassed = $SkipTunnelCheck
$cloudflaredStatus = 'not-checked'

try {
    $resolvedInstallDir = (Resolve-Path -LiteralPath $installDir).Path
    $resolvedPublishDir = if (Test-Path -LiteralPath $publishDir) { (Resolve-Path -LiteralPath $publishDir).Path } else { $publishDir }
    if ($resolvedInstallDir -eq $resolvedPublishDir) {
        $effectivePublishDir = Join-Path $repoRoot 'publish-staging'
        Write-OpsLog -Message "Install directory matches publish output path; using staging publish directory '$effectivePublishDir'" -Level 'WARN' -LogFile $logFile
    }
}
catch {
    Write-OpsLog -Message "Could not resolve install/publish paths for comparison; defaulting publish output to '$effectivePublishDir'" -Level 'WARN' -LogFile $logFile
}

Ensure-Directory -Path $backupRoot
Write-OpsLog -Message "Creating backup: $backupDir" -LogFile $logFile
robocopy $installDir $backupDir /E /R:1 /W:1 /XD logs backups /NFL /NDL /NJH /NJS /NP | Out-Null
$backupCode = $LASTEXITCODE
if ($backupCode -gt 7) {
    Write-OpsLog -Message "Backup failed with robocopy code $backupCode" -Level 'ERROR' -LogFile $logFile
    exit 1
}

try {
    if ($PullLatest) {
        $gitRoot = $repoRoot
        if (-not (Test-Path -LiteralPath (Join-Path $gitRoot '.git'))) {
            $parent = Split-Path $repoRoot -Parent
            if (-not [string]::IsNullOrWhiteSpace($parent) -and (Test-Path -LiteralPath (Join-Path $parent '.git'))) {
                $gitRoot = $parent
            }
        }

        if (Test-Path -LiteralPath (Join-Path $gitRoot '.git')) {
            Write-OpsLog -Message 'Pulling latest changes from git' -LogFile $logFile
            Push-Location $gitRoot
            try {
                git pull
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-OpsLog -Message 'PullLatest was set but repository does not contain .git metadata; skipping pull' -Level 'WARN' -LogFile $logFile
        }
    }

    if (Test-Path -LiteralPath $effectivePublishDir) {
        Remove-Item -Path $effectivePublishDir -Recurse -Force
    }

    Write-OpsLog -Message 'Publishing middleware binaries' -LogFile $logFile
    dotnet publish $projectPath -c $Configuration -r $PublishRuntime --self-contained false -o $effectivePublishDir

    Write-OpsLog -Message "Stopping service '$($serviceInfo.Name)'" -LogFile $logFile
    if (-not (Stop-ServiceSafe -Name $serviceInfo.Name -TimeoutSeconds 45)) {
        throw "Failed to stop service '$($serviceInfo.Name)'"
    }

    $touchedInstall = $true
    Write-OpsLog -Message "Deploying binaries to '$installDir'" -LogFile $logFile
    robocopy $effectivePublishDir $installDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    $deployCode = $LASTEXITCODE
    if ($deployCode -gt 7) {
        throw "Deploy failed with robocopy code $deployCode"
    }

    Write-OpsLog -Message "Starting service '$($serviceInfo.Name)'" -LogFile $logFile
    if (-not (Ensure-ServiceRunning -Name $serviceInfo.Name -TimeoutSeconds 45)) {
        throw "Failed to start service '$($serviceInfo.Name)' after deployment"
    }

    $localLivenessOk = Invoke-ReadinessCheck -Uri $LocalLivenessUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -ExpectedStatus 'healthy' -LogFile $logFile
    if (-not $localLivenessOk) {
        throw "Local liveness check failed after update: $LocalLivenessUrl"
    }
    $localLivenessPassed = $true
    Write-OpsLog -Message 'Local middleware liveness verified' -LogFile $logFile

    $localHealthOk = Invoke-ReadinessCheck -Uri $LocalHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
    if (-not $localHealthOk) {
        Write-OpsLog -Message "Local health check failed; attempting one middleware service restart before rollback" -Level 'WARN' -LogFile $logFile
        if (-not (Stop-ServiceSafe -Name $serviceInfo.Name -TimeoutSeconds 45)) {
            Write-OpsLog -Message "Middleware service '$($serviceInfo.Name)' did not stop cleanly during local-health recovery" -Level 'WARN' -LogFile $logFile
        }

        if (-not (Ensure-ServiceRunning -Name $serviceInfo.Name -TimeoutSeconds 45)) {
            throw "Local health recovery failed: could not restart service '$($serviceInfo.Name)'"
        }

        $localHealthOk = Invoke-ReadinessCheck -Uri $LocalHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
    }

    if (-not $localHealthOk) {
        $rollbackOnFailure = $false
        throw "Local health check failed after update: $LocalHealthUrl"
    }
    $localHealthPassed = $true
    Write-OpsLog -Message 'Local middleware readiness verified' -LogFile $logFile

    if (-not $SkipTunnelCheck) {
        $cloudflaredInfo = Get-ServiceInfoByNames -Names @($CloudflaredPrimaryName, $CloudflaredSecondaryName)
        if ($cloudflaredInfo.Exists) {
            $cloudflaredStatus = $cloudflaredInfo.Status
            Write-OpsLog -Message "Cloudflared service '$($cloudflaredInfo.Name)' status before tunnel check: $cloudflaredStatus" -LogFile $logFile
        }
        else {
            $cloudflaredStatus = 'missing'
            Write-OpsLog -Message "Cloudflared service was not found by names '$CloudflaredPrimaryName'/'$CloudflaredSecondaryName'" -Level 'WARN' -LogFile $logFile
        }

        $tunnelHealthOk = Invoke-ReadinessCheck -Uri $TunnelHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
        if (-not $tunnelHealthOk) {
            Write-OpsLog -Message 'Tunnel health check failed; attempting Cloudflared repair and retry' -Level 'WARN' -LogFile $logFile
            $cloudflaredServiceName = Resolve-CloudflaredServiceName -PrimaryName $CloudflaredPrimaryName -SecondaryName $CloudflaredSecondaryName
            if (-not [string]::IsNullOrWhiteSpace($cloudflaredServiceName)) {
                try {
                    if (-not (Stop-ServiceSafe -Name $cloudflaredServiceName -TimeoutSeconds 30)) {
                        Write-OpsLog -Message "Cloudflared service '$cloudflaredServiceName' did not stop cleanly during repair" -Level 'WARN' -LogFile $logFile
                    }
                }
                catch {
                    Write-OpsLog -Message "Cloudflared stop attempt failed for '$cloudflaredServiceName': $($_.Exception.Message)" -Level 'WARN' -LogFile $logFile
                }

                if (-not (Ensure-ServiceRunning -Name $cloudflaredServiceName -TimeoutSeconds 45)) {
                    Write-OpsLog -Message "Cloudflared service '$cloudflaredServiceName' failed to start during repair" -Level 'WARN' -LogFile $logFile
                }
                else {
                    $cloudflaredStatus = 'Running'
                }
            }
            else {
                Write-OpsLog -Message 'Cloudflared service not found during tunnel repair attempt' -Level 'WARN' -LogFile $logFile
            }

            $tunnelHealthOk = Invoke-ReadinessCheck -Uri $TunnelHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
        }

        if (-not $tunnelHealthOk) {
            $rollbackOnFailure = $false
            throw "Tunnel health check failed after update: $TunnelHealthUrl"
        }

        $tunnelHealthPassed = $true
        Write-OpsLog -Message 'Tunnel readiness verified' -LogFile $logFile
    }

    Write-OpsLog -Message "Verification summary: localLiveness=$localLivenessPassed; localHealth=$localHealthPassed; tunnelHealth=$tunnelHealthPassed; cloudflaredStatus=$cloudflaredStatus" -LogFile $logFile
    Write-OpsLog -Message 'Middleware update completed successfully' -LogFile $logFile
    exit 0
}
catch {
    Write-OpsLog -Message "Update failed: $($_.Exception.Message)" -Level 'ERROR' -LogFile $logFile

    if ($rollbackOnFailure -and $touchedInstall) {
        Write-OpsLog -Message 'Attempting rollback from latest backup' -Level 'WARN' -LogFile $logFile
        try {
            if (-not (Stop-ServiceSafe -Name $serviceInfo.Name -TimeoutSeconds 30)) {
                Write-OpsLog -Message "Service '$($serviceInfo.Name)' did not stop cleanly before rollback" -Level 'WARN' -LogFile $logFile
            }

            robocopy $backupDir $installDir /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            $rollbackCode = $LASTEXITCODE
            if ($rollbackCode -gt 7) {
                throw "Rollback failed with robocopy code $rollbackCode"
            }

            if (-not (Ensure-ServiceRunning -Name $serviceInfo.Name -TimeoutSeconds 45)) {
                throw "Rollback restore succeeded but service '$($serviceInfo.Name)' failed to restart"
            }

            Write-OpsLog -Message 'Rollback completed; service restored' -Level 'WARN' -LogFile $logFile
        }
        catch {
            Write-OpsLog -Message "Rollback failed: $($_.Exception.Message)" -Level 'ERROR' -LogFile $logFile
        }
    }
    else {
        Write-OpsLog -Message "Skipping rollback (rollbackOnFailure=$rollbackOnFailure; touchedInstall=$touchedInstall)" -Level 'WARN' -LogFile $logFile
        if ($touchedInstall) {
            Write-OpsLog -Message "Deployment files remain in place at '$installDir' for post-deploy connectivity troubleshooting" -Level 'WARN' -LogFile $logFile
        }
    }

    exit 1
}
