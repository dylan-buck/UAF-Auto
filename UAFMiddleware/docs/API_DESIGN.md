# UAF Middleware API Design

## Overview

This document outlines the API endpoints needed to process incoming Purchase Orders and create Sales Orders in Sage 100.

## Data Extracted from Purchase Order

| Field | Purpose |
|-------|---------|
| **Customer Name** | Look up customer in Sage |
| **Customer Address** | Secondary lookup verification |
| **Customer Phone** | Tertiary lookup verification |
| **Ship-To Address** | MUST match customer's default ship-to |
| **Items Requested** | Item codes for line items |
| **Quantity per Item** | QuantityOrdered for each line |
| **Price per Item** | Logged/verified (Sage sets actual pricing) |

## Validation Rules Ownership

- Middleware returns matching/scoring facts and Sage execution results.
- Tenant-specific business policy (for example PASS/REJECTED decisions and edge-case transforms) is handled in automation.
- n8n owns UAF rules such as item-code prefix normalization and final routing decisions.

## Ingestion Flow

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐     ┌──────────────┐
│  PDF/Email PO   │────▶│  n8n Parser  │────▶│  UAF Middleware │────▶│   Sage 100   │
│                 │     │              │     │                 │     │              │
│ Extract:        │     │ Send JSON:   │     │ 1. Lookup cust  │     │ Creates:     │
│ • Customer info │     │ • Name       │     │ 2. Get default  │     │ • SO Header  │
│ • Ship-to addr  │     │ • Ship-to    │     │    ship-to      │     │ • SO Lines   │
│ • Items/Qty     │     │ • Items      │     │ 3. Compare addr │     │ • Uses whse  │
│ • Prices        │     │ • Quantities │     │ 4. If match ──▶ │────▶│ • Uses ship  │
│ • Notes/Memos   │     │ • Prices     │     │ 5. If no match  │     │   via method │
└─────────────────┘     │ • Notes      │     │    ──▶ REJECT   │     └──────────────┘
                        └──────────────┘     └─────────────────┘
                                                     │
                                                     ▼ (on rejection)
                                             ┌─────────────────┐
                                             │  Webhook/Alert  │
                                             │  Manual Review  │
                                             └─────────────────┘
```

## Sample Incoming Data (from n8n)

```json
{
  "poNumber": "6814255-00",
  "customer": {
    "name": "United Refrigeration, Inc.",
    "address1": "11401 Roosevelt Blvd.",
    "city": "PHILADELPHIA",
    "state": "PA",
    "zipCode": "19154",
    "phone": "704-637-0555"
  },
  "shipTo": {
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714"
  },
  "notes": "",
  "lines": [
    { "itemCode": "14202", "quantity": 48, "unitPrice": 3.15 },
    { "itemCode": "15202", "quantity": 48, "unitPrice": 3.23 },
    { "itemCode": "16202", "quantity": 240, "unitPrice": 2.98 },
    { "itemCode": "16252", "quantity": 180, "unitPrice": 3.33 },
    { "itemCode": "16242", "quantity": 72, "unitPrice": 3.74 },
    { "itemCode": "18242", "quantity": 180, "unitPrice": 3.90 },
    { "itemCode": "20202", "quantity": 300, "unitPrice": 3.40 },
    { "itemCode": "20252", "quantity": 120, "unitPrice": 3.85 },
    { "itemCode": "20302", "quantity": 24, "unitPrice": 4.35 },
    { "itemCode": "16204", "quantity": 24, "unitPrice": 5.37 }
  ]
}
```

**Notes:**
- `customer` info used for lookup (name, address, phone)
- `shipTo` must match customer's **default** ship-to in Sage
- `unitPrice` is logged but Sage determines actual pricing from customer price level
- `notes` field checked for special instructions requiring manual review

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

### 5. Process Purchase Order (Full Workflow)

**Endpoint:** `POST /api/v1/purchase-orders/process`

**Purpose:** Complete PO processing with validation - creates sales order only if all checks pass

**Request Body:**
```json
{
  "poNumber": "6814255-00",
  "customer": {
    "name": "United Refrigeration, Inc.",
    "address1": "11401 Roosevelt Blvd.",
    "city": "PHILADELPHIA",
    "state": "PA",
    "zipCode": "19154",
    "phone": "704-637-0555"
  },
  "shipTo": {
    "name": "SALI UNITED REFRIGERATION INC",
    "address1": "1912 S. MAIN ST.",
    "city": "SALISBURY",
    "state": "NC",
    "zipCode": "28144-6714"
  },
  "notes": "",
  "lines": [
    { "itemCode": "14202", "quantity": 48, "unitPrice": 3.15 },
    { "itemCode": "15202", "quantity": 48, "unitPrice": 3.23 },
    { "itemCode": "16202", "quantity": 240, "unitPrice": 2.98 }
  ]
}
```

**Response (Success - Order Created):**
```json
{
  "success": true,
  "action": "ORDER_CREATED",
  "salesOrderNumber": "0334499",
  "message": "Sales order created successfully",
  "details": {
    "customerNumber": "01-D3600",
    "customerName": "UNITED REFRIGERATION INC (NC)",
    "shipToCode": "SALI",
    "shipToMatched": true,
    "warehouseCode": "000",
    "shipVia": "UPS GROUND",
    "lineCount": 3,
    "poUnitPrices": [3.15, 3.23, 2.98],
    "sagePrices": [3.15, 3.23, 2.98]
  }
}
```

**Response (Rejected - Ship-To Mismatch):**
```json
{
  "success": false,
  "action": "MANUAL_REVIEW_REQUIRED",
  "errorCode": "SHIPTO_MISMATCH",
  "message": "PO ship-to address does not match customer's default ship-to",
  "webhookSent": true,
  "details": {
    "customerNumber": "01-D3600",
    "customerName": "UNITED REFRIGERATION INC (NC)",
    "defaultShipTo": {
      "shipToCode": "MAIN",
      "address1": "11401 Roosevelt Blvd.",
      "city": "PHILADELPHIA",
      "state": "PA"
    },
    "poShipTo": {
      "address1": "1912 S. MAIN ST.",
      "city": "SALISBURY",
      "state": "NC"
    }
  }
}
```

**Response (Rejected - Special Instructions):**
```json
{
  "success": false,
  "action": "MANUAL_REVIEW_REQUIRED",
  "errorCode": "SPECIAL_INSTRUCTIONS",
  "message": "PO contains notes requiring human verification",
  "webhookSent": true,
  "details": {
    "notes": "PLEASE CALL BEFORE DELIVERY - LOADING DOCK ONLY"
  }
}
```

**Response (Rejected - Customer Not Found):**
```json
{
  "success": false,
  "action": "MANUAL_REVIEW_REQUIRED",
  "errorCode": "CUSTOMER_NOT_FOUND",
  "message": "Could not find matching customer in Sage",
  "webhookSent": true,
  "details": {
    "searchedName": "United Refrigeration, Inc.",
    "searchedAddress": "11401 Roosevelt Blvd., PHILADELPHIA, PA",
    "searchedPhone": "704-637-0555"
  }
}
```

---

### 6. Create Sales Order (Direct - Bypass Validation)

**Endpoint:** `POST /api/v1/sales-orders`

**Purpose:** Direct order creation when customer number is already known (skip lookup/validation)

**Request Body:**
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

**Response:**
```json
{
  "success": true,
  "salesOrderNumber": "0334499",
  "message": "Sales order created successfully"
}
```

**Note:** Sage automatically sets:
- **Warehouse** from ship-to address default
- **Ship Via** from ship-to address default  
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
