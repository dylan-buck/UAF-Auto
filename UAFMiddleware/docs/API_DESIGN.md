# UAF Middleware API Design

## Overview

This document outlines the API endpoints needed to process incoming Purchase Orders (like the United Refrigeration example) and create Sales Orders in Sage 100.

## Ingestion Flow

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐
│  PDF/Email PO   │────▶│  n8n Parser  │────▶│  UAF Middleware │────▶│   Sage 100   │
│ (United Refrig) │     │  (Extract)   │     │  (Validate/API) │     │ (Sales Order)│
└─────────────────┘     └──────────────┘     └─────────────────┘     └──────────────┘
```

## Sample Incoming Data (from n8n)

```json
{
  "poNumber": "6814255-00",
  "customerName": "SALI UNITED REFRIGERATION INC",
  "shipTo": {
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714"
  },
  "lines": [
    { "itemCode": "14202", "quantity": 48 },
    { "itemCode": "15202", "quantity": 48 },
    { "itemCode": "16202", "quantity": 240 },
    { "itemCode": "16252", "quantity": 180 },
    { "itemCode": "16242", "quantity": 72 },
    { "itemCode": "18242", "quantity": 180 },
    { "itemCode": "20202", "quantity": 300 },
    { "itemCode": "20252", "quantity": 120 },
    { "itemCode": "20302", "quantity": 24 },
    { "itemCode": "16204", "quantity": 24 }
  ]
}
```

**Note:** We do NOT use pricing from the PO - Sage determines pricing based on customer price level. 
We do NOT use their warehouse code - Sage's ship-to address determines our warehouse and freight method.

---

## API Endpoints

### 1. Customer Search by Name

**Endpoint:** `GET /api/v1/customers/search`

**Purpose:** Find customers by name to handle similar names like:
- United Refrigeration, Inc (NC)
- United Refrigeration, Inc (SC)

**Query Parameters:**
- `name` (required): Customer name to search for
- `city` (optional): Filter by city
- `state` (optional): Filter by state
- `limit` (optional, default: 10): Max results

**Example Request:**
```
GET /api/v1/customers/search?name=United+Refrigeration&state=NC
```

**Example Response:**
```json
{
  "customers": [
    {
      "customerNumber": "01-D3600",
      "customerName": "UNITED REFRIGERATION INC (NC)",
      "address1": "1912 S. MAIN ST.",
      "city": "SALISBURY",
      "state": "NC",
      "zipCode": "28144",
      "defaultWarehouseCode": "000",
      "priceLevel": "1",
      "taxSchedule": "NC"
    },
    {
      "customerNumber": "01-D3601",
      "customerName": "UNITED REFRIGERATION INC (SC)",
      "address1": "456 Industrial Blvd",
      "city": "GREENVILLE",
      "state": "SC",
      "zipCode": "29601",
      "defaultWarehouseCode": "001",
      "priceLevel": "1",
      "taxSchedule": "SC"
    }
  ],
  "totalCount": 2
}
```

**BOI Objects Used:**
- `AR_Customer_svc` - Customer lookup and filtering

---

### 2. Get Customer Details with Ship-To Addresses

**Endpoint:** `GET /api/v1/customers/{customerNumber}`

**Purpose:** Get full customer details including all ship-to addresses

**Example Request:**
```
GET /api/v1/customers/01-D3600
```

**Example Response:**
```json
{
  "customerNumber": "01-D3600",
  "customerName": "UNITED REFRIGERATION INC (NC)",
  "arDivisionNo": "01",
  "status": "A",
  "billingAddress": {
    "address1": "11401 Roosevelt Blvd.",
    "city": "PHILADELPHIA",
    "state": "PA",
    "zipCode": "19154"
  },
  "defaultShipTo": {
    "shipToCode": "SALI",
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714",
    "warehouseCode": "000"
  },
  "shipToAddresses": [
    {
      "shipToCode": "SALI",
      "name": "SALI UNITED REFRIGERATION INC",
      "address1": "1912 S. MAIN ST.",
      "city": "SALISBURY",
      "state": "NC",
      "zipCode": "28144-6714",
      "warehouseCode": "000",
      "isDefault": true
    },
    {
      "shipToCode": "CHAR",
      "name": "CHARLOTTE LOCATION",
      "address1": "789 Distribution Way",
      "city": "CHARLOTTE",
      "state": "NC",
      "zipCode": "28201",
      "warehouseCode": "001",
      "isDefault": false
    }
  ],
  "priceLevel": "1",
  "taxSchedule": "NC",
  "termsCode": "NET30"
}
```

**BOI Objects Used:**
- `AR_Customer_svc` - Customer master data
- `SO_ShipToAddress_svc` - Ship-to addresses for customer

---

### 3. Validate Ship-To Address

**Endpoint:** `POST /api/v1/customers/{customerNumber}/validate-shipto`

**Purpose:** Check if incoming ship-to address matches a registered address

**Request Body:**
```json
{
  "name": "SALI UNITED REFRIGERATION INC",
  "address1": "1912 S. MAIN ST.",
  "city": "SALISBURY",
  "state": "NC",
  "zipCode": "28144-6714"
}
```

**Response (Match Found):**
```json
{
  "matched": true,
  "shipToCode": "SALI",
  "warehouseCode": "000",
  "matchConfidence": 0.95,
  "matchedAddress": {
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714"
  }
}
```

**Response (No Match - Requires Manual Review):**
```json
{
  "matched": false,
  "reason": "Address not found in customer ship-to addresses",
  "suggestedAction": "MANUAL_REVIEW",
  "closestMatches": [
    {
      "shipToCode": "SALI",
      "matchConfidence": 0.72,
      "differences": ["zipCode mismatch: 28144 vs 28144-6714"]
    }
  ]
}
```

---

### 4. Validate Item Exists

**Endpoint:** `GET /api/v1/items/{itemCode}`

**Purpose:** Verify item code exists before order creation (optional pre-validation)

**Example Request:**
```
GET /api/v1/items/14202
```

**Example Response:**
```json
{
  "itemCode": "14202",
  "description": "MERV 10 PLEATED FILTER STANDARD CAPACITY",
  "uom": "EA",
  "active": true
}
```

**BOI Objects Used:**
- `CI_Item_svc` - Item master data

---

### 5. Create Sales Order

**Endpoint:** `POST /api/v1/sales-orders`

**Purpose:** Create sales order - Sage determines pricing, warehouse, and freight from ship-to

**Request Body (Option A - Customer Number Known):**
```json
{
  "customerNumber": "01-D3600",
  "poNumber": "6814255-00",
  "shipToCode": "SALI",
  "lines": [
    { "itemCode": "14202", "quantity": 48 },
    { "itemCode": "15202", "quantity": 48 },
    { "itemCode": "16202", "quantity": 240 }
  ]
}
```

**Request Body (Option B - Lookup by Name/Address):**
```json
{
  "poNumber": "6814255-00",
  "customerName": "SALI UNITED REFRIGERATION INC",
  "shipToAddress": {
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714"
  },
  "lines": [
    { "itemCode": "14202", "quantity": 48 },
    { "itemCode": "15202", "quantity": 48 },
    { "itemCode": "16202", "quantity": 240 }
  ]
}
```

**Response (Success):**
```json
{
  "success": true,
  "salesOrderNumber": "0334499",
  "message": "Sales order created successfully",
  "customerNumber": "01-D3600",
  "shipToCode": "SALI",
  "warehouseCode": "000",
  "freightMethod": "UPS GROUND"
}
```

**Response (Ship-To Mismatch - Needs Review):**
```json
{
  "success": false,
  "errorCode": "SHIPTO_MISMATCH",
  "errorMessage": "Ship-to address does not match any registered address for this customer",
  "requiresManualReview": true,
  "webhookSent": true,
  "customerNumber": "01-D3600",
  "incomingAddress": {
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC"
  },
  "registeredAddresses": [
    { "shipToCode": "SALI", "address1": "1912 SOUTH MAIN STREET", "city": "SALISBURY" }
  ]
}
```

**Note:** Sage automatically sets:
- **Warehouse** from ship-to address default
- **Freight method** from ship-to address default  
- **Pricing** from customer price level
- **Tax schedule** from customer/ship-to settings

---

## Webhook for Manual Review (Future State)

When `validateShipTo: true` and address doesn't match, send webhook:

**Webhook Payload:**
```json
{
  "event": "ORDER_REQUIRES_REVIEW",
  "timestamp": "2025-12-23T23:45:00Z",
  "orderData": {
    "poNumber": "6814255-00",
    "customerNumber": "01-D3600",
    "customerName": "UNITED REFRIGERATION INC (NC)",
    "reason": "SHIPTO_MISMATCH",
    "incomingShipTo": {
      "name": "SALI UNITED REFRIGERATION INC",
      "address1": "1912 S. MAIN ST.",
      "city": "SALISBURY",
      "state": "NC"
    },
    "registeredShipTos": [
      { "shipToCode": "SALI", "address1": "1912 SOUTH MAIN STREET" }
    ]
  },
  "reviewUrl": "https://your-app.com/review/order/abc123"
}
```

---

## BOI Objects Reference

| Object | Purpose |
|--------|---------|
| `AR_Customer_svc` | Customer lookup and master data |
| `SO_ShipToAddress_svc` | Ship-to addresses for customers |
| `CI_Item_svc` | Item/product master data |
| `IM_ItemWarehouse_svc` | Inventory by warehouse |
| `SO_SalesOrder_bus` | Create/update sales orders |
| `SY_Company_svc` | Company/warehouse list |

---

## Implementation Priority

1. **Phase 1 (MVP - Current):** 
   - ✅ Sales order creation with customer number
   - Customer search by name
   - Customer details with ship-to addresses

2. **Phase 2 (Enhanced Lookup):**
   - Sales order creation with name/address lookup (auto-resolve customer)
   - Ship-to address matching and validation
   - Item code validation

3. **Phase 3 (Automation):**
   - Webhook notifications for manual review cases
   - n8n integration endpoint for direct PO ingestion

