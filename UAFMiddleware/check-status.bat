@echo off
:: UAF Sage Middleware - Check Status

echo ============================================
echo UAF Sage Middleware - Status Check
echo ============================================
echo.

set "SERVICE_NAME=UAFSageMiddleware"

:: Check service status
echo Service Status:
sc query %SERVICE_NAME% 2>nul | findstr "STATE"
if %errorLevel% neq 0 (
    echo   Service is NOT installed
)

echo.
echo Cloudflared Status:
sc query Cloudflared 2>nul | findstr "STATE" >nul
if %errorLevel% equ 0 (
    sc query Cloudflared 2>nul | findstr "STATE"
) else (
    sc query cloudflared 2>nul | findstr "STATE"
    if %errorLevel% neq 0 (
        echo   Cloudflared service is NOT installed
    )
)

echo.
echo Startup Task Status (UAF-VerifyServices):
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $t = Get-ScheduledTask -TaskName 'UAF-VerifyServices' -ErrorAction Stop; $i = Get-ScheduledTaskInfo -TaskName 'UAF-VerifyServices'; Write-Host ('  State: ' + $t.State + ', LastRun: ' + $i.LastRunTime + ', Result: ' + $i.LastTaskResult) } catch { Write-Host '  Task is NOT registered' }"

echo.
echo Testing API Health Endpoint...
echo.

:: Try to check health endpoint using PowerShell
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:3000/health' -UseBasicParsing -TimeoutSec 5; Write-Host 'API Response:' $response.Content } catch { Write-Host 'API not responding: ' $_.Exception.Message }"

echo.
echo ============================================
echo.
pause




