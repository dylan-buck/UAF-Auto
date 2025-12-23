@echo off
echo Compiling Sage100_BOI_Working.cs...
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /out:Sage100_BOI_Working.exe Sage100_BOI_Working.cs

if %errorlevel% neq 0 (
    echo Compilation failed!
    pause
    exit /b %errorlevel%
)

echo Compilation successful. Running Sage100_BOI_Working.exe...
.\Sage100_BOI_Working.exe
pause
