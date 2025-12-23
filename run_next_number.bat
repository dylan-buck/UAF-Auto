@echo off
echo Compiling Sage100_BOI_GetNextNumber.cs...
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /out:Sage100_BOI_GetNextNumber.exe Sage100_BOI_GetNextNumber.cs

if %errorlevel% neq 0 (
    echo Compilation failed!
    pause
    exit /b %errorlevel%
)

echo Compilation successful. Running Sage100_BOI_GetNextNumber.exe...
.\Sage100_BOI_GetNextNumber.exe
pause
