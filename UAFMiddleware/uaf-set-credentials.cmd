@echo off
setlocal

if /I "%~1"=="--elevated" (
    shift
    goto run
)

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'cmd.exe' -Verb RunAs -ArgumentList '/c \"\"%~f0\" --elevated %*\"'"
    endlocal & exit /b %errorlevel%
)

:run
set "SCRIPT=%~dp0ops\set-credentials.ps1"
if not exist "%SCRIPT%" (
    echo ERROR: Missing script %SCRIPT%
    endlocal & exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
