# UAF Sage Middleware - API Test Script
# Run this in PowerShell to test the API

param(
    [string]$BaseUrl = "http://localhost:3000",
    [string]$ApiKey = "",
    [string]$CustomerNumber = "",
    [string]$ItemCode = ""
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "UAF Sage Middleware - API Test" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Health Check
Write-Host "Test 1: Basic Health Check" -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET -TimeoutSec 10
    Write-Host "  Status: $($healthResponse.status)" -ForegroundColor Green
    Write-Host "  Uptime: $($healthResponse.uptime)"
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 2: Readiness Check
Write-Host "Test 2: Readiness Check (Sage 100 Connection)" -ForegroundColor Yellow
try {
    $readyResponse = Invoke-RestMethod -Uri "$BaseUrl/health/ready" -Method GET -TimeoutSec 30
    Write-Host "  Status: $($readyResponse.status)" -ForegroundColor Green
    Write-Host "  Sage 100: $($readyResponse.sage100)"
    if ($readyResponse.details) {
        Write-Host "  Available Sessions: $($readyResponse.details.availableSessions)"
        Write-Host "  Active Sessions: $($readyResponse.details.activeSessions)"
    }
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 3: API Test Endpoint
Write-Host "Test 3: Sales Order API Test Endpoint" -ForegroundColor Yellow
try {
    $headers = @{ "Content-Type" = "application/json" }
    if ($ApiKey) { $headers["X-API-Key"] = $ApiKey }
    
    $testResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/sales-orders/test" -Method GET -Headers $headers -TimeoutSec 10
    Write-Host "  Message: $($testResponse.message)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 4: Create Sales Order (only if customer/item provided)
if ($CustomerNumber -and $ItemCode) {
    Write-Host "Test 4: Create Test Sales Order" -ForegroundColor Yellow
    Write-Host "  Customer: $CustomerNumber"
    Write-Host "  Item: $ItemCode"
    
    $orderBody = @{
        customerNumber = $CustomerNumber
        poNumber = "API-TEST-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        comment = "Test order from PowerShell script"
        lines = @(
            @{
                itemCode = $ItemCode
                quantity = 1
            }
        )
    } | ConvertTo-Json -Depth 3
    
    try {
        $headers = @{ "Content-Type" = "application/json" }
        if ($ApiKey) { $headers["X-API-Key"] = $ApiKey }
        
        $orderResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/sales-orders" -Method POST -Body $orderBody -Headers $headers -TimeoutSec 60
        
        if ($orderResponse.success) {
            Write-Host "  SUCCESS!" -ForegroundColor Green
            Write-Host "  Sales Order Number: $($orderResponse.salesOrderNumber)" -ForegroundColor Green
            Write-Host "  Message: $($orderResponse.message)"
        } else {
            Write-Host "  FAILED: $($orderResponse.errorMessage)" -ForegroundColor Red
            Write-Host "  Error Code: $($orderResponse.errorCode)"
        }
    } catch {
        Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
} else {
    Write-Host "Test 4: Create Sales Order (SKIPPED)" -ForegroundColor Yellow
    Write-Host "  Provide -CustomerNumber and -ItemCode to test order creation"
    Write-Host "  Example: .\test-api.ps1 -CustomerNumber 'CUST001' -ItemCode 'ITEM001'"
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Tests Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan




