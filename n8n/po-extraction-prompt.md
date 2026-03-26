# PO PDF Extraction Prompt for n8n

## System Prompt (for Claude/GPT node)

```
You are a purchase order data extraction assistant. Extract structured data from purchase order PDFs and return valid JSON only.

Extract the following fields:
- poNumber: The PO number (e.g., "6852008-00")
- poDate: The PO date in YYYY-MM-DD format
- customerName: The company name from "Sold To" section
- shipToName: The name from "Ship To" section
- shipToAddress1: First line of ship-to address
- shipToAddress2: Second line if present, otherwise empty string
- shipToCity: City from ship-to address
- shipToState: State abbreviation (2 letters)
- shipToZipCode: ZIP code from ship-to address
- shipToCode: The code next to "Ship To" label if present (e.g., "494")
- specialInstructions: Any special instructions or notes (empty string if none)
- lineItems: Array of line items with:
  - lineNumber: Line number
  - itemCode: Product/item code
  - description: Item description
  - quantity: Quantity ordered (number)
  - unitPrice: Unit price (number, no currency symbol)
  - unitOfMeasure: Unit of measure (e.g., "ea")

Return ONLY valid JSON, no explanation or markdown.
```

## User Prompt Template

```
Extract the purchase order data from this PDF and return as JSON:

{pdf_content}
```

## Expected Output Format

```json
{
  "poNumber": "6852008-00",
  "poDate": "2025-12-23",
  "customerName": "United Refrigeration Inc.",
  "shipToName": "CONC UNITED REFRIGERATION INC",
  "shipToAddress1": "281 EXECUTIVE PARK DR.",
  "shipToAddress2": "",
  "shipToCity": "CONCORD",
  "shipToState": "NC",
  "shipToZipCode": "28025-1895",
  "shipToCode": "494",
  "specialInstructions": "",
  "lineItems": [
    {
      "lineNumber": 1,
      "itemCode": "FT10101",
      "description": "MERV 10 PLEATED FILTER STANDARD CAPACITY",
      "quantity": 24,
      "unitPrice": 2.20,
      "unitOfMeasure": "ea"
    },
    {
      "lineNumber": 2,
      "itemCode": "FT12121",
      "description": "MERV 10 PLEATED FILTER STANDARD CAPACITY",
      "quantity": 36,
      "unitPrice": 2.78,
      "unitOfMeasure": "ea"
    }
  ]
}
```

## Mapping to Customer Resolution API

The extracted data maps to the middleware API as follows:

```json
{
  "customerName": "{{ $json.customerName }}",
  "shipToAddress": {
    "name": "{{ $json.shipToName }}",
    "address1": "{{ $json.shipToAddress1 }}",
    "city": "{{ $json.shipToCity }}",
    "state": "{{ $json.shipToState }}",
    "zipCode": "{{ $json.shipToZipCode }}"
  }
}
```

## SKU Transformation Rule (Automation Layer)

Apply this in n8n (Set or Code node) before creating the sales order payload.

Rule precedence:
1. If line item description contains `Poly` (case-insensitive), keep full SKU.
2. Else if description contains `Merv 10` (case-insensitive), remove leading `FT` only when SKU starts with `FT`.
3. Else keep SKU unchanged.

Edge-case defaults:
- If both `Poly` and `Merv 10` are present: `Poly` wins (keep full SKU).
- If `Merv 10` is present but SKU does not start with `FT`: leave unchanged.
- Only remove a leading `FT`; do not alter interior `FT`.

### n8n Code Node Snippet

```javascript
const ENABLE_UAF_SKU_TRANSFORM = true;

function normalizeText(value) {
  return String(value || "").toLowerCase();
}

function trimSku(value) {
  return String(value || "").trim();
}

function startsWithFt(sku) {
  return sku.toUpperCase().startsWith("FT");
}

function transformSku(itemCode, description) {
  const originalItemCode = trimSku(itemCode);
  const desc = normalizeText(description);
  const hasPoly = desc.includes("poly");
  const hasMerv10 = desc.includes("merv 10");

  if (hasPoly) {
    return { originalItemCode, transformedItemCode: originalItemCode, transformRuleApplied: "POLY_KEEP_FULL_SKU" };
  }

  if (hasMerv10) {
    if (startsWithFt(originalItemCode)) {
      return { originalItemCode, transformedItemCode: originalItemCode.slice(2), transformRuleApplied: "MERV10_DROP_LEADING_FT" };
    }
    return { originalItemCode, transformedItemCode: originalItemCode, transformRuleApplied: "MERV10_NO_FT_PREFIX_NO_CHANGE" };
  }

  return { originalItemCode, transformedItemCode: originalItemCode, transformRuleApplied: "NONE" };
}

return $input.all().map((item) => {
  const json = item.json || {};
  const lineItems = Array.isArray(json.lineItems) ? json.lineItems : [];

  if (!ENABLE_UAF_SKU_TRANSFORM) {
    return { json };
  }

  return {
    json: {
      ...json,
      lineItems: lineItems.map((line) => {
        const transform = transformSku(line.itemCode, line.description);
        return {
          ...line,
          itemCode: transform.transformedItemCode,
          skuTransform: transform, // optional audit field
        };
      }),
    },
  };
});
```

## Special Instruction Keywords to Flag

These phrases in the PO should trigger MANUAL_REVIEW:
- "CALL BEFORE SHIPPING"
- "SPECIAL INSTRUCTIONS"
- "HOLD FOR APPROVAL"
- "CONFIRM PRICING"
- "DO NOT SHIP UNTIL"
- "CUSTOMER PICKUP"
- "WILL CALL"

## n8n Workflow Nodes

1. **Email Trigger** → receives email with PDF attachment
2. **Extract from File** → extracts PDF content as text
3. **AI Node (Claude)** → uses prompt above to parse into JSON
4. **Set/Code Node** → transforms AI output for API call (includes SKU rule above)
5. **HTTP Request** → calls customer resolution API
6. **Switch Node** → branches based on recommendation
