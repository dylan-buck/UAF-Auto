# UAF Services Boot Verification Script
# Run as Scheduled Task: trigger on startup, delay 2 minutes
#
# To install as scheduled task (run as Admin):
#   $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File C:\UAF-Auto\UAFMiddleware\verify-services.ps1"
#   $trigger = New-ScheduledTaskTrigger -AtStartup
#   $trigger.Delay = "PT2M"
#   $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
#   Register-ScheduledTask -TaskName "UAF-VerifyServices" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User "SYSTEM"

$logFile = "C:\UAF-Auto\logs\boot-verify.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

function Write-Log($msg) {
    $entry = "[$timestamp] $msg"
    Write-Output $entry
    Add-Content -Path $logFile -Value $entry
}

# Ensure log directory exists
New-Item -ItemType Directory -Force -Path (Split-Path $logFile) | Out-Null

Write-Log "=== Boot Verification Started ==="

# Check UAFSageMiddleware Windows Service
$middlewareService = Get-Service -Name "UAFSageMiddleware" -ErrorAction SilentlyContinue
if ($middlewareService) {
    if ($middlewareService.Status -eq "Running") {
        Write-Log "OK: UAFSageMiddleware service is running"
    } else {
        Write-Log "WARN: UAFSageMiddleware status is $($middlewareService.Status) - attempting start..."
        Start-Service -Name "UAFSageMiddleware" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5
        $middlewareService.Refresh()
        Write-Log "UAFSageMiddleware is now $($middlewareService.Status)"
    }
} else {
    Write-Log "ERROR: UAFSageMiddleware service not found"
}

# Check Cloudflared Windows Service
$cloudflaredService = Get-Service -Name "Cloudflared" -ErrorAction SilentlyContinue
if (-not $cloudflaredService) {
    $cloudflaredService = Get-Service -Name "cloudflared" -ErrorAction SilentlyContinue
}
if ($cloudflaredService) {
    if ($cloudflaredService.Status -eq "Running") {
        Write-Log "OK: Cloudflared service is running"
    } else {
        Write-Log "WARN: Cloudflared status is $($cloudflaredService.Status) - attempting start..."
        Start-Service -Name $cloudflaredService.Name -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5
        $cloudflaredService.Refresh()
        Write-Log "Cloudflared is now $($cloudflaredService.Status)"
    }
} else {
    Write-Log "ERROR: Cloudflared service not found"
}

# Wait for middleware to be ready, then test health endpoint
Write-Log "Waiting 10 seconds for services to initialize..."
Start-Sleep -Seconds 10

try {
    $response = Invoke-RestMethod -Uri "http://localhost:3000/health/ready" -TimeoutSec 10
    Write-Log "OK: Middleware health check passed (status: $($response.status))"
} catch {
    Write-Log "ERROR: Middleware health check failed - $($_.Exception.Message)"
}

# Test tunnel connectivity
try {
    $tunnelResponse = Invoke-RestMethod -Uri "https://sage.uaf-automation.uk/health/ready" -TimeoutSec 15
    Write-Log "OK: Tunnel connectivity verified (status: $($tunnelResponse.status))"
} catch {
    Write-Log "WARN: Tunnel not yet reachable - $($_.Exception.Message)"
    Write-Log "This may resolve in 30-60 seconds as cloudflared establishes the connection"
}

Write-Log "=== Boot Verification Complete ==="
