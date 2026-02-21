param(
    [string]$MiddlewareServiceName = 'UAFSageMiddleware',
    [string]$CloudflaredPrimaryName = 'Cloudflared',
    [string]$CloudflaredSecondaryName = 'cloudflared',
    [string]$StartupTaskName = 'UAF-VerifyServices',
    [string]$LocalHealthUrl = 'http://localhost:3000/health/ready',
    [string]$TunnelHealthUrl = 'https://sage.uaf-automation.uk/health/ready',
    [int]$HealthRetries = 6,
    [int]$HealthDelaySeconds = 5,
    [switch]$Repair,
    [switch]$SkipTunnelCheck,
    [switch]$AllowMissingCloudflared
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptRoot 'lib/common.ps1')

$logFile = New-LogFilePath -Name 'verify-stack.log'
$originalArgs = @($MyInvocation.UnboundArguments)

if ($Repair) {
    Ensure-Admin -ScriptPath $PSCommandPath -Arguments $originalArgs
}

Write-OpsLog -Message 'Starting stack verification' -LogFile $logFile

$failures = New-Object System.Collections.Generic.List[string]

$middlewareService = Get-ServiceInfoByNames -Names @($MiddlewareServiceName)
if (-not $middlewareService.Exists) {
    $failures.Add("Middleware service '$MiddlewareServiceName' is missing")
}
else {
    Write-OpsLog -Message "Middleware service '$($middlewareService.Name)' status: $($middlewareService.Status)" -LogFile $logFile
    if ($Repair -and $middlewareService.Status -ne 'Running') {
        if (Ensure-ServiceRunning -Name $middlewareService.Name -TimeoutSeconds 30) {
            Write-OpsLog -Message "Middleware service '$($middlewareService.Name)' started by repair action" -LogFile $logFile
        }
        else {
            $failures.Add("Middleware service '$($middlewareService.Name)' failed to start")
        }
    }
    elseif ($middlewareService.Status -ne 'Running') {
        $failures.Add("Middleware service '$($middlewareService.Name)' is not running")
    }
}

$cloudflaredService = Get-ServiceInfoByNames -Names @($CloudflaredPrimaryName, $CloudflaredSecondaryName)
if (-not $cloudflaredService.Exists) {
    if ($AllowMissingCloudflared) {
        Write-OpsLog -Message 'Cloudflared service is missing, but allowed by flag' -Level 'WARN' -LogFile $logFile
    }
    else {
        $failures.Add('Cloudflared service is missing')
    }
}
else {
    Write-OpsLog -Message "Cloudflared service '$($cloudflaredService.Name)' status: $($cloudflaredService.Status)" -LogFile $logFile
    if ($Repair -and $cloudflaredService.Status -ne 'Running') {
        if (Ensure-ServiceRunning -Name $cloudflaredService.Name -TimeoutSeconds 30) {
            Write-OpsLog -Message "Cloudflared service '$($cloudflaredService.Name)' started by repair action" -LogFile $logFile
        }
        else {
            $failures.Add("Cloudflared service '$($cloudflaredService.Name)' failed to start")
        }
    }
    elseif ($cloudflaredService.Status -ne 'Running') {
        $failures.Add("Cloudflared service '$($cloudflaredService.Name)' is not running")
    }
}

try {
    $task = Get-ScheduledTask -TaskName $StartupTaskName -ErrorAction SilentlyContinue
    if ($null -eq $task) {
        $failures.Add("Startup task '$StartupTaskName' is missing")
    }
    else {
        $taskInfo = Get-ScheduledTaskInfo -TaskName $StartupTaskName
        Write-OpsLog -Message "Startup task '$StartupTaskName' state: $($task.State), last run: $($taskInfo.LastRunTime), result: $($taskInfo.LastTaskResult)" -LogFile $logFile
    }
}
catch {
    $failures.Add("Unable to inspect startup task '$StartupTaskName': $($_.Exception.Message)")
}

$localHealthOk = Invoke-ReadinessCheck -Uri $LocalHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
if (-not $localHealthOk) {
    $failures.Add("Local health check failed: $LocalHealthUrl")
}

if (-not $SkipTunnelCheck -and -not $AllowMissingCloudflared) {
    $tunnelHealthOk = Invoke-ReadinessCheck -Uri $TunnelHealthUrl -Retries $HealthRetries -DelaySeconds $HealthDelaySeconds -LogFile $logFile
    if (-not $tunnelHealthOk) {
        $failures.Add("Tunnel health check failed: $TunnelHealthUrl")
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-OpsLog -Message $failure -Level 'ERROR' -LogFile $logFile
    }

    Write-OpsLog -Message "Stack verification failed with $($failures.Count) issue(s)" -Level 'ERROR' -LogFile $logFile
    exit 1
}

Write-OpsLog -Message 'Stack verification passed' -LogFile $logFile
exit 0
