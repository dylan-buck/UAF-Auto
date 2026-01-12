# UAF Sage Middleware - Customer Resolution Test Script
# Tests the recent ship-to matching fixes

param(
    [string]$BaseUrl = "http://localhost:3000",
    [string]$ApiKey = ""
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Customer Resolution Test Suite" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$headers = @{ "Content-Type" = "application/json" }
if ($ApiKey) { $headers["X-API-Key"] = $ApiKey }

# Test 1: Health Check
Write-Host "Test 0: Health Check" -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET -TimeoutSec 10
    Write-Host "  Status: $($healthResponse.status)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "  FAILED: API not responding at $BaseUrl" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure the service is running!" -ForegroundColor Red
    exit 1
}

# Test 1: Known good customer - United Refrigeration with exact address
Write-Host "Test 1: Known Good Match - United Refrigeration (Greensboro)" -ForegroundColor Yellow
Write-Host "  Expected: HIGH confidence, AUTO_PROCESS recommendation" -ForegroundColor Gray
$test1Body = @{
    customerName = "United Refrigeration, Inc."
    shipToAddress = @{
        address1 = "3707 ALLIANCE DR"
        city = "GREENSBORO"
        state = "NC"
        zipCode = "27407"
    }
} | ConvertTo-Json -Depth 3

try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/resolve" -Method POST -Body $test1Body -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    Write-Host "  Result: $($response.recommendation)" -ForegroundColor $(if ($response.recommendation -eq "AUTO_PROCESS") { "Green" } else { "Yellow" })
    Write-Host "  Confidence: $([math]::Round($response.confidence * 100, 1))%" -ForegroundColor $(if ($response.confidence -ge 0.8) { "Green" } else { "Yellow" })
    Write-Host "  Customer: $($response.bestMatch.customerNumber) - $($response.bestMatch.customerName)"
    Write-Host "  Ship-To Code: $($response.bestMatch.matchedShipToCode)"
    Write-Host "  Is Default Ship-To: $($response.bestMatch.isDefaultShipTo)"
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"

    if ($response.bestMatch.scoreBreakdown) {
        Write-Host "  Score Breakdown:" -ForegroundColor Gray
        Write-Host "    Name: $([math]::Round($response.bestMatch.scoreBreakdown.nameScore * 100, 1))%"
        Write-Host "    Ship-To: $([math]::Round($response.bestMatch.scoreBreakdown.shipToScore * 100, 1))%"
        Write-Host "    Default Bonus: $([math]::Round($response.bestMatch.scoreBreakdown.defaultShipToBonus * 100, 1))%"
    }
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 2: Same customer, different ship-to (test non-default matching)
Write-Host "Test 2: Same Customer, Non-Default Ship-To Address" -ForegroundColor Yellow
Write-Host "  Expected: Match found, but isDefaultShipTo=false" -ForegroundColor Gray
$test2Body = @{
    customerName = "United Refrigeration"
    shipToAddress = @{
        city = "CHARLOTTE"
        state = "NC"
    }
} | ConvertTo-Json -Depth 3

try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/resolve" -Method POST -Body $test2Body -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    Write-Host "  Result: $($response.recommendation)" -ForegroundColor $(if ($response.resolved) { "Green" } else { "Yellow" })
    Write-Host "  Confidence: $([math]::Round($response.confidence * 100, 1))%"
    Write-Host "  Customer: $($response.bestMatch.customerNumber) - $($response.bestMatch.customerName)"
    Write-Host "  Ship-To Code: $($response.bestMatch.matchedShipToCode)"
    Write-Host "  Is Default Ship-To: $($response.bestMatch.isDefaultShipTo)"
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 3: Partial name match (tests fuzzy matching)
Write-Host "Test 3: Fuzzy Name Matching" -ForegroundColor Yellow
Write-Host "  Expected: Should still find United Refrigeration" -ForegroundColor Gray
$test3Body = @{
    customerName = "UNITED REFRIG"
    shipToAddress = @{
        city = "GREENSBORO"
        state = "NC"
    }
} | ConvertTo-Json -Depth 3

try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/resolve" -Method POST -Body $test3Body -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    Write-Host "  Result: $($response.recommendation)"
    Write-Host "  Confidence: $([math]::Round($response.confidence * 100, 1))%"
    Write-Host "  Customer: $($response.bestMatch.customerNumber) - $($response.bestMatch.customerName)"
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 4: Name with variations (Inc., LLC, etc.)
Write-Host "Test 4: Name Normalization (Inc. vs no suffix)" -ForegroundColor Yellow
$test4Body = @{
    customerName = "United Refrigeration Inc"
    shipToAddress = @{
        address1 = "3707 Alliance Drive"
        city = "Greensboro"
        state = "NC"
        zipCode = "27407"
    }
} | ConvertTo-Json -Depth 3

try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/resolve" -Method POST -Body $test4Body -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    Write-Host "  Result: $($response.recommendation)"
    Write-Host "  Confidence: $([math]::Round($response.confidence * 100, 1))%"
    Write-Host "  Customer: $($response.bestMatch.customerNumber) - $($response.bestMatch.customerName)"
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 5: Unknown customer (should return low confidence)
Write-Host "Test 5: Unknown Customer (Negative Test)" -ForegroundColor Yellow
Write-Host "  Expected: REJECTED or MANUAL_REVIEW with low confidence" -ForegroundColor Gray
$test5Body = @{
    customerName = "ACME Widgets Corporation"
    shipToAddress = @{
        city = "SPRINGFIELD"
        state = "XX"
    }
} | ConvertTo-Json -Depth 3

try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/resolve" -Method POST -Body $test5Body -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    $color = if ($response.recommendation -eq "REJECTED" -or $response.confidence -lt 0.7) { "Green" } else { "Yellow" }
    Write-Host "  Result: $($response.recommendation)" -ForegroundColor $color
    Write-Host "  Confidence: $([math]::Round($response.confidence * 100, 1))%"
    Write-Host "  Candidates found: $($response.candidates.Count)"
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 6: Customer search endpoint
Write-Host "Test 6: Customer Search API" -ForegroundColor Yellow
try {
    $start = Get-Date
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/customers/search?name=United&state=NC&limit=5" -Method GET -Headers $headers -TimeoutSec 60
    $elapsed = ((Get-Date) - $start).TotalSeconds

    Write-Host "  Found: $($response.totalCount) customers" -ForegroundColor Green
    Write-Host "  Time: $([math]::Round($elapsed, 2))s"
    if ($response.customers) {
        foreach ($cust in $response.customers | Select-Object -First 3) {
            Write-Host "    - $($cust.customerNumber): $($cust.customerName)"
        }
    }
    Write-Host ""
} catch {
    Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Tests Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  - Test 1: Exact match should have >80% confidence"
Write-Host "  - Test 2: Non-default ship-to should still match but flag isDefaultShipTo=false"
Write-Host "  - Test 3: Fuzzy name matching should work with partial names"
Write-Host "  - Test 4: Name normalization should handle Inc., LLC, etc."
Write-Host "  - Test 5: Unknown customers should be REJECTED or low confidence"
Write-Host ""
