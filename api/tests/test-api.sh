#!/bin/bash

# Test script for UAF Sage API
# Requires: curl, jq (for JSON formatting)

API_URL="${API_URL:-http://localhost:3000}"
API_KEY="${API_KEY:-your_api_key_here}"

echo "🧪 Testing UAF Sage API"
echo "========================"
echo ""

# Test 1: Health Check
echo "1️⃣  Testing health check..."
curl -s "${API_URL}/health" | jq .
echo ""

# Test 2: Readiness Check
echo "2️⃣  Testing readiness check..."
curl -s "${API_URL}/health/ready" | jq .
echo ""

# Test 3: Create Sales Order (Synchronous)
echo "3️⃣  Testing sales order creation (sync)..."
curl -s -X POST \
  "${API_URL}/api/v1/sales-orders" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: ${API_KEY}" \
  -d @tests/fixtures/sample-order.json | jq .
echo ""

# Test 4: Create Sales Order (Asynchronous)
echo "4️⃣  Testing sales order creation (async)..."
RESPONSE=$(curl -s -X POST \
  "${API_URL}/api/v1/sales-orders" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: ${API_KEY}" \
  -d '{
    "customerNumber": "CUST001",
    "poNumber": "PO-2025-002",
    "lines": [
      {
        "itemCode": "FILTER-001",
        "quantity": 5
      }
    ],
    "async": true
  }')

echo "$RESPONSE" | jq .
JOB_ID=$(echo "$RESPONSE" | jq -r '.jobId')
echo ""

# Test 5: Check Job Status
if [ "$JOB_ID" != "null" ] && [ -n "$JOB_ID" ]; then
  echo "5️⃣  Testing job status check..."
  sleep 2  # Wait a bit for processing
  curl -s "${API_URL}/api/v1/sales-orders/${JOB_ID}" \
    -H "X-API-Key: ${API_KEY}" | jq .
  echo ""
fi

echo "✅ Tests completed"

