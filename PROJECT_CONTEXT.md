# UAF Sage 100 Middleware API - Complete Project Context

## Project Overview

This project implements a middleware API that integrates with Sage 100 to automate the creation of Sales Orders from incoming Purchase Orders. The system is designed for low-volume processing (~100 orders/month) and provides intelligent customer resolution, validation, and automated order processing.

## Core Objectives

- **Parse incoming PO data**: Extract customer name, address, phone, line items, and notes
- **Perform customer lookup**: Identify correct Sage 100 customer using fuzzy matching algorithms
- **Validate ship-to address**: Ensure PO ship-to matches customer's default ship-to address
- **Retrieve warehouse/shipping defaults**: Use customer's default warehouse and ship-via settings
- **Validate items and pricing**: Ensure items exist and pricing is correct
- **Handle special instructions**: Reject orders requiring human verification
- **Create Sales Orders**: Automate order creation in Sage 100 when all validations pass

## Architecture Overview

### Technology Stack

- **Node.js API Service**: Express.js with Redis queuing
- **.NET BOI Service**: C# service using COM Interop to Sage 100 Business Object Interface
- **Redis**: Job queuing and status tracking
- **Docker**: Containerized deployment
- **Winston**: Structured logging

### Key Services

#### 1. Node.js API Service (`/api`)
- **Purpose**: RESTful API endpoint for receiving PO data
- **Features**:
  - Request validation and authentication
  - Job queuing and status tracking
  - Async processing capabilities
  - Health monitoring endpoints

#### 2. .NET BOI Service (`/sage-boi-service`)
- **Purpose**: Direct integration with Sage 100 via COM objects
- **Features**:
  - Session pool management for Sage 100 connections
  - Sales order creation via `SO_SalesOrder_bus`
  - Customer lookup via `AR_Customer_svc`
  - Ship-to address validation via `SO_ShipToAddress_svc`

#### 3. Customer Resolution Engine
- **Purpose**: Intelligent matching of PO customer data to Sage 100 records
- **Algorithm**:
  - Fuzzy name matching (handles "Inc.", "(NC)" variations)
  - Address normalization and comparison
  - Phone number normalization
  - Weighted scoring system (name: 50%, address: 30%, phone: 20%)
  - Confidence thresholds (90%+ = auto-approve, 70-89% = manual review, <70% = reject)

## Implementation Details

### Session Management

**Sage 100 Connection Pool**:
- Maintains 1-3 active COM sessions (reduced from 3 to 1 due to threading issues)
- Session validation and automatic invalidation on errors
- Thread-safe session borrowing and returning

**Key Methods**:
```csharp
// Session creation
var script = new ProvideX.Script();
script.Init(serverPath);
var session = script.NewObject("SY_Session");
session.nSetUser(username, password);
session.nSetCompany(companyCode);
```

### Customer Resolution Logic

**Search Phase**:
- Scans `AR_Customer_svc` records with name/city/state filters
- Fuzzy matching on customer names
- Returns top candidates sorted by relevance

**Resolution Phase**:
- Loads full customer details including default ship-to code
- Retrieves all ship-to addresses for each candidate
- Compares PO ship-to address against customer's ship-to records
- Finds best address match using component-wise comparison

**Scoring Algorithm**:
```javascript
Score = (NameMatch * 0.5) + (AddressMatch * 0.3) + (PhoneMatch * 0.2)

NameMatch: Fuzzy string similarity (0-1)
AddressMatch: Component matching (city, state, zip, street)
PhoneMatch: Normalized phone comparison
```

### Sales Order Creation

**BOI Integration**:
- Uses `SO_SalesOrder_bus` for order creation
- Follows specific field setting order: ItemCode → Quantity → WarehouseCode
- Handles line items via `oLines` property
- Validates all operations with `sLastErrorMsg` checking

**Key Steps**:
1. Get next sales order number via `nGetNextSalesOrderNo`
2. Set order key with `nSetKey(orderNumber)`
3. Set header fields (customer, PO number, dates)
4. Add line items with `nAddLine()` and field setting
5. Commit with `nWrite()`

## API Design

### Endpoints

#### Sales Orders
```http
POST /api/v1/sales-orders
Content-Type: application/json
X-API-Key: your-api-key

{
  "customerNumber": "01-A0075",
  "purchaseOrderNumber": "PO-12345",
  "orderDate": "2024-01-15",
  "shipToAddress": {
    "name": "Customer Name",
    "address1": "123 Main St",
    "city": "Anytown",
    "state": "NC",
    "zipCode": "12345"
  },
  "lines": [
    {
      "itemCode": "ITEM001",
      "quantity": 10,
      "unitPrice": 25.00
    }
  ]
}
```

#### Customer Resolution
```http
POST /api/v1/customers/resolve
{
  "customerName": "United Refrigeration, Inc.",
  "shipToAddress": {
    "address1": "3707 ALLIANCE DR",
    "city": "GREENSBORO",
    "state": "NC",
    "zipCode": "27407"
  }
}
```

#### Job Status
```http
GET /api/v1/sales-orders/{jobId}
```

### Response Formats

**Success Response**:
```json
{
  "jobId": "so_1234567890",
  "status": "queued|processing|completed|failed",
  "result": {
    "sageOrderNumber": "0334496",
    "success": true
  },
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Customer Resolution Response**:
```json
{
  "resolved": true,
  "confidence": 0.95,
  "recommendation": "AUTO_APPROVE",
  "bestMatch": {
    "customerNumber": "01-D3601",
    "customerName": "UNITED REFRIGERATION INC (NC)",
    "score": 0.95,
    "matchedShipToCode": "3707",
    "isDefaultShipTo": true,
    "warehouseCode": "002",
    "shipVia": "COMPANY TRUCK"
  }
}
```

## Validation Rules

### Auto-Processing Criteria
- Customer resolution confidence ≥ 90%
- Ship-to address matches customer's default ship-to
- All line items exist in Sage 100
- No special instructions requiring human review

### Rejection Criteria
- Customer resolution confidence < 70%
- Ship-to address doesn't match any customer ship-to
- PO contains notes like "CALL BEFORE SHIPPING", "SPECIAL INSTRUCTIONS", etc.
- Invalid item codes or quantities
- Customer account is inactive/hold

## Challenges & Solutions

### COM Interop Issues
**Problem**: Dynamic dispatch with COM objects unreliable
**Solution**: Use `Type.InvokeMember` for explicit late binding

### Session Threading
**Problem**: COM threading issues with session pool > 1
**Solution**: Reduced pool size to 1 session

### Error Message Retrieval
**Problem**: C# dynamic objects don't expose Sage error messages
**Solution**: Check `sLastErrorMsg` properties on BOI objects

### Customer Name Variations
**Problem**: "United Refrigeration, Inc." vs "UNITED REFRIGERATION INC (NC)"
**Solution**: Fuzzy string matching with normalization

### Address Matching
**Problem**: Flexible address formats and partial matches
**Solution**: Component-wise address comparison (city/state/zip priority)

## Current Project Status

### Completed Components
- ✅ Node.js API service with Express
- ✅ .NET BOI service with session management
- ✅ Customer search and resolution logic
- ✅ Sales order creation via BOI
- ✅ Docker containerization
- ✅ Redis job queuing
- ✅ Comprehensive logging
- ✅ Health monitoring endpoints

### In Progress
- 🔄 Performance optimization (customer search takes ~18 seconds)
- 🔄 Ship-to address validation refinement
- 🔄 Production deployment configuration

### Known Issues
- Customer resolution scanning limited to 500 records (needs optimization)
- Ship-to matching occasionally misses valid addresses
- Session invalidation logic may need refinement

## Deployment Architecture

### Docker Compose Setup
```yaml
services:
  api:         # Node.js API (port 3000)
  boi-service: # .NET BOI service (port 5000)
  redis:       # Job queue and status storage
```

### Environment Configuration
```env
NODE_ENV=production
API_PORT=3000
API_KEY=secure-api-key

SAGE_USERNAME=dyl
SAGE_PASSWORD=encrypted-password
SAGE_COMPANY=TST
SAGE_SERVER_PATH=\\server\Sage\MAS90\Home

REDIS_HOST=redis
REDIS_PORT=6379
```

## Testing & Validation

### Test Scenarios
1. **Happy Path**: Valid PO creates order successfully
2. **Customer Mismatch**: Low confidence customer resolution
3. **Invalid Address**: Ship-to doesn't match customer records
4. **Invalid Items**: Non-existent item codes
5. **BOI Errors**: Sage 100 connectivity issues
6. **Queue Processing**: Async job handling

### Sample Test Data
```json
{
  "customerNumber": "01-A0075",
  "purchaseOrderNumber": "API-TEST-001",
  "shipToAddress": {
    "address1": "3707 ALLIANCE DR",
    "city": "GREENSBORO",
    "state": "NC",
    "zipCode": "27407"
  },
  "lines": [
    {
      "itemCode": "14202",
      "quantity": 2,
      "unitPrice": 125.00
    }
  ]
}
```

## Future Enhancements

### Phase 2 Features
- Webhook notifications for failed orders
- Batch order processing
- Enhanced error reporting
- Order status synchronization
- Inventory validation before order creation

### Monitoring & Alerting
- Email notifications for critical failures
- Dashboard for order processing metrics
- Log aggregation and analysis
- Automated health checks

## Key Technical Decisions

### Synchronous Processing
- Low volume justifies immediate processing
- Async option available for high-volume scenarios
- Redis used for job status tracking regardless

### Session Pool Size = 1
- Sage 100 BOI threading limitations
- Low volume doesn't require concurrency
- Simpler error handling and debugging

### Fuzzy Matching Algorithm
- Weighted scoring system balances accuracy vs. flexibility
- Configurable thresholds for different confidence levels
- Component-wise address matching for better precision

### Error Handling Strategy
- Fail fast on validation errors
- Detailed logging for debugging
- Graceful degradation with session recovery
- User-friendly error messages via API

## Development Workflow

### Local Development
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f api

# Test API
curl -X POST http://localhost:3000/api/v1/sales-orders \
  -H "Content-Type: application/json" \
  -d @test-order.json
```

### Testing Scripts
- `test-api.sh`: Automated API testing
- `TestApp/Program.cs`: Direct BOI testing
- `TestSalesOrder.vbs`: VBScript BOI validation

This comprehensive middleware provides reliable, automated sales order processing while maintaining strict validation controls and detailed audit trails for business compliance.