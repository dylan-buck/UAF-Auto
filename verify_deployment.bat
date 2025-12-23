@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo UAF Auto - Deployment Verification Script
echo ========================================================

REM 1. Check Docker
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Docker is not running! Please start Docker Desktop.
    pause
    exit /b 1
)
echo [OK] Docker is running.

REM 2. Build and Start
echo.
echo Building and starting services...
docker-compose up -d --build
if %errorlevel% neq 0 (
    echo [ERROR] Failed to start services.
    pause
    exit /b 1
)

echo.
echo Waiting for services to initialize (30 seconds)...
timeout /t 30 /nobreak >nul

REM 3. Check Health
echo.
echo Checking API health...
curl -s http://localhost:3000/health | findstr "healthy" >nul
if %errorlevel% neq 0 (
    echo [ERROR] API is not healthy. Check logs with: docker-compose logs api
    exit /b 1
)
echo [OK] API is healthy.

echo.
echo Checking BOI Service health...
curl -s http://localhost:5000/health | findstr "Healthy" >nul
if %errorlevel% neq 0 (
    echo [ERROR] BOI Service is not healthy. Check logs with: docker-compose logs sage-boi-service
    exit /b 1
)
echo [OK] BOI Service is healthy.

REM 4. Send Test Order
echo.
echo Sending test sales order...
set JSON_BODY={\"customerNumber\": \"00-UAF\", \"poNumber\": \"TEST-AUTO-001\", \"orderDate\": \"20251119\", \"lines\": [{\"itemCode\": \"100-20X20X1\", \"quantity\": 10}]}

REM Note: You might need to adjust the customer number and item code to valid ones in your system
REM Creating a temporary json file for curl
echo %JSON_BODY% > test_order.json

for /f "tokens=*" %%a in ('curl -s -X POST -H "Content-Type: application/json" -H "X-API-Key: dev_key_12345" -d @test_order.json http://localhost:3000/api/v1/sales-orders') do set RESPONSE=%%a
echo Response: !RESPONSE!

REM Extract Job ID (simple string parsing for batch)
REM This is a bit hacky in batch, but sufficient for a quick test
echo.
echo Check the response above. If it contains "jobId", the order is queued.
echo.
echo To monitor the logs and see the order creation:
echo docker-compose logs -f sage-boi-service

del test_order.json
pause
