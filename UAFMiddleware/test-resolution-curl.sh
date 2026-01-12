#!/bin/bash
# UAF Sage Middleware - Customer Resolution Test (curl)
# Run from any machine against the Windows server

BASE_URL="${1:-http://localhost:3000}"
API_KEY="${2:-}"

echo "============================================"
echo "Customer Resolution Test Suite (curl)"
echo "Target: $BASE_URL"
echo "============================================"
echo ""

# Headers
HEADERS="-H 'Content-Type: application/json'"
if [ -n "$API_KEY" ]; then
    HEADERS="$HEADERS -H 'X-API-Key: $API_KEY'"
fi

# Test 0: Health Check
echo "Test 0: Health Check"
HEALTH=$(curl -s "$BASE_URL/health")
echo "  Response: $HEALTH"
if [ -z "$HEALTH" ]; then
    echo "  FAILED: API not responding"
    exit 1
fi
echo ""

# Test 1: Known good match - United Refrigeration
echo "Test 1: Known Good Match - United Refrigeration (Greensboro)"
echo "  Expected: HIGH confidence, AUTO_PROCESS"
RESPONSE=$(curl -s -X POST "$BASE_URL/api/v1/customers/resolve" \
    -H "Content-Type: application/json" \
    -d '{
        "customerName": "United Refrigeration, Inc.",
        "shipToAddress": {
            "address1": "3707 ALLIANCE DR",
            "city": "GREENSBORO",
            "state": "NC",
            "zipCode": "27407"
        }
    }')
echo "  Response:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
echo ""

# Test 2: Fuzzy name matching
echo "Test 2: Fuzzy Name Match - UNITED REFRIG (partial)"
RESPONSE=$(curl -s -X POST "$BASE_URL/api/v1/customers/resolve" \
    -H "Content-Type: application/json" \
    -d '{
        "customerName": "UNITED REFRIG",
        "shipToAddress": {
            "city": "GREENSBORO",
            "state": "NC"
        }
    }')
echo "  Response:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
echo ""

# Test 3: Unknown customer (negative test)
echo "Test 3: Unknown Customer (Negative Test)"
echo "  Expected: REJECTED or low confidence"
RESPONSE=$(curl -s -X POST "$BASE_URL/api/v1/customers/resolve" \
    -H "Content-Type: application/json" \
    -d '{
        "customerName": "ACME Widgets Corporation",
        "shipToAddress": {
            "city": "SPRINGFIELD",
            "state": "XX"
        }
    }')
echo "  Response:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
echo ""

# Test 4: Customer Search
echo "Test 4: Customer Search API"
RESPONSE=$(curl -s "$BASE_URL/api/v1/customers/search?name=United&state=NC&limit=5")
echo "  Response:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
echo ""

echo "============================================"
echo "Tests Complete"
echo "============================================"
