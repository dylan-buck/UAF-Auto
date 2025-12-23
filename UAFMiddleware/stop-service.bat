@echo off
:: UAF Sage Middleware - Stop Service

echo Stopping UAF Sage Middleware...

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

sc stop UAFSageMiddleware

if %errorLevel% equ 0 (
    echo Service stopped successfully.
) else (
    echo Failed to stop service. It may already be stopped.
)

echo.
pause




