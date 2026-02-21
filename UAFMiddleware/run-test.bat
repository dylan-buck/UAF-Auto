@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "TEST_SCRIPT=%SCRIPT_DIR%test-api.ps1"

if not exist "%TEST_SCRIPT%" (
    echo ERROR: Missing test script: %TEST_SCRIPT%
    endlocal & exit /b 1
)

echo ============================================
echo UAF Sage Middleware - API Smoke Test
echo ============================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%TEST_SCRIPT%" %*
set "EXITCODE=%ERRORLEVEL%"

if %EXITCODE% neq 0 (
    echo.
    echo Smoke test failed with exit code %EXITCODE%.
)

echo.
pause
endlocal & exit /b %EXITCODE%
