@echo off
:: UAF Sage Middleware - View Logs

set "LOGS_DIR=%~dp0logs"

if not exist "%LOGS_DIR%" (
    echo No logs folder found yet.
    echo Logs will be created after the service runs.
    echo.
    pause
    exit /b 0
)

echo Opening logs folder: %LOGS_DIR%
explorer "%LOGS_DIR%"





