@echo off
setlocal enabledelayedexpansion

echo ============================================
echo UAF Sage Middleware - Build Package
echo ============================================
echo.

set "SCRIPT_DIR=%~dp0"
set "PROJECT_DIR=%SCRIPT_DIR%src"
set "OUTPUT_DIR=%SCRIPT_DIR%publish"
set "PACKAGE_DIR=%SCRIPT_DIR%UAF-Middleware-v1.0"

echo Checking .NET SDK...
dotnet --version
if !errorlevel! neq 0 goto :nodotnet

echo.
echo Cleaning previous builds...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"

echo.
echo Restoring NuGet packages...
dotnet restore "%PROJECT_DIR%\UAFMiddleware.csproj"
if !errorlevel! neq 0 goto :restorefailed

echo.
echo Building and publishing...
dotnet publish "%PROJECT_DIR%\UAFMiddleware.csproj" -c Release -r win-x64 --self-contained false -o "%OUTPUT_DIR%"
if !errorlevel! neq 0 goto :buildfailed

echo.
echo Creating deployment package...
mkdir "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%\logs"

echo Copying files...
xcopy "%OUTPUT_DIR%\*" "%PACKAGE_DIR%\" /E /Y /Q

copy "%SCRIPT_DIR%install-service.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%uninstall-service.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%start-service.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%stop-service.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%view-logs.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%check-status.bat" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%README.txt" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%test-api.ps1" "%PACKAGE_DIR%\" >nul 2>&1
copy "%SCRIPT_DIR%test-order.json" "%PACKAGE_DIR%\" >nul 2>&1

echo.
echo ============================================
echo BUILD COMPLETE!
echo ============================================
echo.
echo Package location: %PACKAGE_DIR%
echo.
echo Next: Edit appsettings.json with your credentials,
echo then run install-service.bat as Administrator.
echo.
pause
goto :eof

:nodotnet
echo.
echo ERROR: .NET SDK not found!
echo Install from: https://dotnet.microsoft.com/download/dotnet/8.0
pause
goto :eof

:restorefailed
echo.
echo ERROR: Package restore failed!
pause
goto :eof

:buildfailed
echo.
echo ERROR: Build failed!
pause
goto :eof
