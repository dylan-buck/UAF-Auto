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
echo Testing API Health Endpoint...
echo.

:: Try to check health endpoint using PowerShell
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:3000/health' -UseBasicParsing -TimeoutSec 5; Write-Host 'API Response:' $response.Content } catch { Write-Host 'API not responding: ' $_.Exception.Message }"

echo.
echo ============================================
echo.
pause




