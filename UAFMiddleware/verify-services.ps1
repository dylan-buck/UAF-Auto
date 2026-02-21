param(
    [string]$MiddlewareServiceName = 'UAFSageMiddleware',
    [string]$CloudflaredPrimaryName = 'Cloudflared',
    [string]$CloudflaredSecondaryName = 'cloudflared',
    [string]$LocalHealthUrl = 'http://localhost:3000/health/ready',
    [string]$TunnelHealthUrl = 'https://sage.uaf-automation.uk/health/ready',
    [string]$LogFile = 'C:\UAF-Auto\logs\boot-verify.log'
)

# UAF Services Boot Verification Script
# Suggested startup task registration (run elevated once):
#   $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File C:\UAF-Auto\UAFMiddleware\verify-services.ps1"
#   $trigger = New-ScheduledTaskTrigger -AtStartup
#   $trigger.Delay = "PT2M"
#   $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
#   Register-ScheduledTask -TaskName "UAF-VerifyServices" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User "SYSTEM"

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Log {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [ValidateSet('INFO', 'WARN', 'ERROR')]
        [string]$Level = 'INFO'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $entry = "[$timestamp] [$Level] $Message"
    Write-Output $entry
    Add-Content -Path $LogFile -Value $entry
}

function Ensure-ServiceRunning {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName
    )

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        return $false
    }

    if ($service.Status -ne 'Running') {
        Write-Log -Level 'WARN' -Message "$ServiceName status is $($service.Status) - attempting start"
        Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5
        $service.Refresh()
    }

    if ($service.Status -eq 'Running') {
        Write-Log -Message "$ServiceName service is running"
        return $true
    }

    Write-Log -Level 'ERROR' -Message "$ServiceName service failed to reach running state"
    return $false
}

$logDir = Split-Path -Parent $LogFile
if (-not (Test-Path -LiteralPath $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

$hadErrors = $false

Write-Log -Message '=== Boot Verification Started ==='

if (-not (Ensure-ServiceRunning -ServiceName $MiddlewareServiceName)) {
    Write-Log -Level 'ERROR' -Message "$MiddlewareServiceName service not found or failed to start"
    $hadErrors = $true
}

$cloudflaredName = $CloudflaredPrimaryName
if (-not (Get-Service -Name $cloudflaredName -ErrorAction SilentlyContinue)) {
    $cloudflaredName = $CloudflaredSecondaryName
}

if (-not (Ensure-ServiceRunning -ServiceName $cloudflaredName)) {
    Write-Log -Level 'ERROR' -Message 'Cloudflared service not found or failed to start'
    $hadErrors = $true
}

Write-Log -Message 'Waiting 10 seconds for services to initialize'
Start-Sleep -Seconds 10

try {
    $response = Invoke-RestMethod -Uri $LocalHealthUrl -TimeoutSec 10
    if ($response.status -eq 'ready') {
        Write-Log -Message "Local middleware health check passed (status=$($response.status))"
    }
    else {
        Write-Log -Level 'ERROR' -Message "Local middleware health check returned unexpected status '$($response.status)'"
        $hadErrors = $true
    }
}
catch {
    Write-Log -Level 'ERROR' -Message "Local middleware health check failed - $($_.Exception.Message)"
    $hadErrors = $true
}

try {
    $tunnelResponse = Invoke-RestMethod -Uri $TunnelHealthUrl -TimeoutSec 15
    if ($tunnelResponse.status -eq 'ready') {
        Write-Log -Message "Tunnel connectivity verified (status=$($tunnelResponse.status))"
    }
    else {
        Write-Log -Level 'WARN' -Message "Tunnel check returned status '$($tunnelResponse.status)'"
    }
}
catch {
    Write-Log -Level 'WARN' -Message "Tunnel not yet reachable - $($_.Exception.Message)"
}

Write-Log -Message '=== Boot Verification Complete ==='

if ($hadErrors) {
    exit 1
}

exit 0
