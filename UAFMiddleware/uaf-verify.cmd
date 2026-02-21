@echo off
setlocal
set "SCRIPT=%~dp0ops\verify-stack.ps1"

if not exist "%SCRIPT%" (
    echo ERROR: Missing script %SCRIPT%
    endlocal & exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
