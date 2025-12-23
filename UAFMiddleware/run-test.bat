@echo off
cd /d "%~dp0"

echo ===========================================
echo Sage 100 BOI Sales Order Test
echo ===========================================
echo.

REM Find csc.exe from .NET Framework
set CSC=
for /f "delims=" %%i in ('dir /s /b "C:\Windows\Microsoft.NET\Framework64\v4*\csc.exe" 2^>nul') do set CSC=%%i
if "%CSC%"=="" (
    for /f "delims=" %%i in ('dir /s /b "C:\Windows\Microsoft.NET\Framework\v4*\csc.exe" 2^>nul') do set CSC=%%i
)

if "%CSC%"=="" (
    echo ERROR: Could not find csc.exe
    echo Please ensure .NET Framework 4.x is installed
    pause
    exit /b 1
)

echo Compiling with: %CSC%
echo.

"%CSC%" /nologo /target:exe /out:TestSalesOrder.exe TestSalesOrder.cs
if %errorlevel% neq 0 (
    echo.
    echo Compilation failed!
    pause
    exit /b 1
)

echo Compilation successful!
echo.
echo Running test...
echo.

TestSalesOrder.exe %*

pause
