@echo off
:: UAF Sage Middleware - Build and Package Script
:: Run this on a Windows machine with .NET 8.0 SDK installed

echo ============================================
echo UAF Sage Middleware - Build Package
echo ============================================
echo.

set "SCRIPT_DIR=%~dp0"
set "PROJECT_DIR=%SCRIPT_DIR%src"
set "OUTPUT_DIR=%SCRIPT_DIR%publish"
set "PACKAGE_DIR=%SCRIPT_DIR%UAF-Middleware-v1.0"

:: Check if dotnet is installed
dotnet --version >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo.
    echo Please install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Choose ".NET SDK" (not just Runtime) for Windows x64
    echo.
    pause
    exit /b 1
)

echo .NET SDK found: 
dotnet --version
echo.

:: Clean previous builds
echo Cleaning previous builds...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"

:: Restore packages
echo.
echo Restoring NuGet packages...
dotnet restore "%PROJECT_DIR%\UAFMiddleware.csproj"

if %errorLevel% neq 0 (
    echo ERROR: Package restore failed!
    echo Check your internet connection and try again.
    pause
    exit /b 1
)

:: Build and publish
echo.
echo Building and publishing for Windows...
dotnet publish "%PROJECT_DIR%\UAFMiddleware.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -o "%OUTPUT_DIR%"

if %errorLevel% neq 0 (
    echo ERROR: Build failed!
    echo Check the error messages above.
    pause
    exit /b 1
)

:: Create clean package directory
echo.
echo Creating deployment package...
mkdir "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%\logs"

:: Copy published application files
echo Copying application files...
xcopy "%OUTPUT_DIR%\*" "%PACKAGE_DIR%\" /E /Y /Q

:: Copy batch scripts
echo Copying scripts...
copy "%SCRIPT_DIR%install-service.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%uninstall-service.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%start-service.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%stop-service.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%view-logs.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%check-status.bat" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%README.txt" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%test-api.ps1" "%PACKAGE_DIR%\" >nul
copy "%SCRIPT_DIR%test-order.json" "%PACKAGE_DIR%\" >nul

echo.
echo ============================================
echo BUILD COMPLETE!
echo ============================================
echo.
echo Package created at:
echo %PACKAGE_DIR%
echo.
echo Package contents:
echo ------------------
dir /b "%PACKAGE_DIR%"
echo.
echo ============================================
echo NEXT STEPS:
echo ============================================
echo.
echo 1. The package is ready in: %PACKAGE_DIR%
echo.
echo 2. To install:
echo    - Open %PACKAGE_DIR%
echo    - Edit appsettings.json if needed
echo    - Right-click install-service.bat
echo    - Select "Run as administrator"
echo.
echo 3. To test:
echo    - Open browser to http://localhost:3000/health
echo    - Run test-api.ps1 in PowerShell
echo.

pause

