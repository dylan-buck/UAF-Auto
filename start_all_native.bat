@echo off
echo ========================================================
echo UAF Auto - Native Startup Script
echo ========================================================

REM 1. Start Redis (Assumes Docker is installed, easiest way to get Redis)
echo Starting Redis...
docker run -d -p 6379:6379 --name uaf-redis redis
if %errorlevel% neq 0 (
    echo Redis container might already be running, continuing...
)

REM 2. Build BOI Service
echo.
echo Building BOI Service...
cd sage-boi-service
dotnet build
if %errorlevel% neq 0 (
    echo [ERROR] Failed to build BOI Service.
    pause
    exit /b 1
)

REM 3. Start BOI Service in a new window
echo.
echo Starting BOI Service (Port 5000)...
start "UAF BOI Service" dotnet run --project SageBOI.Api/SageBOI.Api.csproj

REM 4. Install and Start API
echo.
echo Installing API dependencies...
cd ../api
call npm install

echo.
echo Starting API Service (Port 3000)...
echo The API will be available at http://localhost:3000
npm start

pause
