@echo off
:: UAF Sage Middleware - Service Uninstallation Script
:: Run this script as Administrator

echo ============================================
echo UAF Sage Middleware - Service Uninstallation
echo ============================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

set "SERVICE_NAME=UAFSageMiddleware"

:: Check if service exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% neq 0 (
    echo Service %SERVICE_NAME% is not installed.
    echo.
    pause
    exit /b 0
)

:: Stop the service
echo Stopping service...
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 /nobreak >nul

:: Delete the service
echo Removing service...
sc delete %SERVICE_NAME%

if %errorLevel% equ 0 (
    echo.
    echo ============================================
    echo SUCCESS! Service has been uninstalled.
    echo ============================================
    echo.
    echo You can now delete the installation folder.
    echo.
) else (
    echo.
    echo WARNING: There was an issue uninstalling the service.
    echo Try restarting Windows and running this script again.
    echo.
)

pause




