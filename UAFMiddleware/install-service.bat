@echo off
:: UAF Sage Middleware - Service Installation Script
:: Run this script as Administrator

echo ============================================
echo UAF Sage Middleware - Service Installation
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

:: Get the directory where this script is located
set "INSTALL_DIR=%~dp0"
set "EXE_PATH=%INSTALL_DIR%UAFMiddleware.exe"
set "SERVICE_NAME=UAFSageMiddleware"
set "DISPLAY_NAME=UAF Sage Middleware"

:: Check if executable exists
if not exist "%EXE_PATH%" (
    echo ERROR: UAFMiddleware.exe not found in %INSTALL_DIR%
    echo Make sure all files are extracted correctly.
    echo.
    pause
    exit /b 1
)

echo Installing service from: %EXE_PATH%
echo.

:: Stop existing service if running
echo Checking for existing service...
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Stopping existing service...
    sc stop %SERVICE_NAME% >nul 2>&1
    timeout /t 3 /nobreak >nul
    
    echo Removing existing service...
    sc delete %SERVICE_NAME% >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: Create the service
echo Creating Windows Service...
sc create %SERVICE_NAME% ^
    binPath= "\"%EXE_PATH%\"" ^
    DisplayName= "%DISPLAY_NAME%" ^
    start= auto ^
    obj= "LocalSystem"

if %errorLevel% neq 0 (
    echo ERROR: Failed to create service!
    echo.
    pause
    exit /b 1
)

:: Configure service description
sc description %SERVICE_NAME% "UAF Sage Middleware - Automated sales order processing for Sage 100"

:: Configure recovery options (restart on failure)
echo Configuring automatic restart on failure...
sc failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/10000/restart/30000

:: Start the service
echo Starting service...
sc start %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo WARNING: Service created but failed to start.
    echo Check the logs folder for error details.
    echo You can start manually: sc start %SERVICE_NAME%
    echo.
    pause
    exit /b 1
)

:: Wait a moment and check status
timeout /t 3 /nobreak >nul
sc query %SERVICE_NAME% | find "RUNNING" >nul

if %errorLevel% equ 0 (
    echo.
    echo ============================================
    echo SUCCESS! Service installed and running.
    echo ============================================
    echo.
    echo Service Name: %SERVICE_NAME%
    echo Display Name: %DISPLAY_NAME%
    echo API URL: http://localhost:3000
    echo.
    echo To test: Open browser to http://localhost:3000/health
    echo.
    echo Manage via:
    echo   - Windows Services (services.msc)
    echo   - start-service.bat / stop-service.bat
    echo.
) else (
    echo.
    echo WARNING: Service installed but may not be running correctly.
    echo Check logs folder for details.
    echo.
)

pause




