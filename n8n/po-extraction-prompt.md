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
4. **Set Node** → transforms AI output for API call
5. **HTTP Request** → calls customer resolution API
6. **Switch Node** → branches based on recommendation
