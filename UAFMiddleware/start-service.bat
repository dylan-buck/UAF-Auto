@echo off
:: UAF Sage Middleware - Start Service

echo Starting UAF Sage Middleware...

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

sc start UAFSageMiddleware

if %errorLevel% equ 0 (
    echo Service started successfully.
    echo.
    echo API available at: http://localhost:3000/health
) else (
    echo Failed to start service. Check Windows Event Viewer for details.
)

echo.
pause





