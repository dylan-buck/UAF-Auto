# Sage 100 2025 API Expansion and MCP Research

Date: 2026-06-04

## Executive Summary

The current UAF middleware is a focused Sage 100 BOI integration for purchase-order automation. It exposes:

- Sales orders: create order and read order details.
- Customers: search, get details, validate ship-to, resolve customer/ship-to match.
- Inventory: validate/check item code endpoints, but implementation is currently pass-through and relies on sales-order creation to catch invalid items.
- Health/readiness checks around the ProvideX/Sage session pool.

Sage 100 has a much broader object surface. UAF is now on Sage 100 2025, so the 2025 File Layouts and Object Reference should be the implementation baseline. In the 2025 object-reference search index, the distribution/accounting/manufacturing modules most relevant to this middleware surfaced roughly 2,136 object-reference entries across AP, AR, BM, BR, CI, CM, GL, IM, PM, PO, RA, and SO. Across all indexed 2025 modules, the docs surfaced roughly 3,606 entries. That includes report, UI, update, helper, business, and service classes, so it should not become a one-to-one public REST surface.

Recommended direction:

1. Expand the middleware by business capability, not by raw BOI object count.
2. Add high-value read/query endpoints first for AI use.
3. Add tightly controlled write endpoints only where there is a clear workflow, validation, audit logging, idempotency, and permission model.
4. Build the MCP server as a safe tool facade over the middleware, not as direct COM/BOI access from ChatGPT or Claude.
5. Include a read-only generic BOI/service query capability for long-tail AI questions, but keep generic write disabled.

## Current Middleware Coverage

Active code is in `UAFMiddleware/src`.

| Area | Current endpoints | Sage objects currently used or implied | Notes |
| --- | --- | --- | --- |
| Sales Order | `POST /api/v1/sales-orders`, `GET /api/v1/sales-orders/{salesOrderNumber}/details` | `SO_SalesOrder_bus`, `SO_SalesOrder_ui`, line child object | Core workflow is implemented. Missing update, delete/cancel, quote/order history, invoices, shipments, payments, memos, duplicate PO search. |
| Customer | `GET /api/v1/customers/search`, `GET /api/v1/customers/{customerNumber}`, `POST /api/v1/customers/{customerNumber}/validate-shipto`, `POST /api/v1/customers/resolve` | `AR_Customer_svc`, `SO_ShipToAddress_svc` | Good fit for PO automation. Missing contacts, memos, open invoices, sales history, credit status, customer documents, CRUD. |
| Inventory | `POST /api/v1/inventory/validate`, `GET /api/v1/inventory/check/{itemCode}` | Pass-through today | Highest-value gap. Use `CI_ItemCode_bus/svc`, `IM_ItemWarehouse_svc`, alias/alternate item objects, price code, item/vendor history. |
| System | `/health`, `/health/ready` | `SY_Session`, ProvideX session pool | Good baseline. Missing object discovery, company/module/license info, role/security diagnostics. |

## Implementation Status in This Expansion Pass

The middleware now includes API-side scopes so MCP can be read-only without being the only enforcement layer. The legacy `ApiKey` remains backward-compatible. New scoped keys can use `read`, `create`, `modify`, `finance`, and `admin`; `ReadOnlyMode` strips every non-read scope at runtime.

New endpoints added:

- `GET /api/v1/items/search`
- `GET /api/v1/items/{itemCode}`
- `GET /api/v1/items/{itemCode}/availability`
- `POST /api/v1/items/availability`
- `GET /api/v1/items/{itemCode}/aliases`
- `GET /api/v1/items/{itemCode}/alternates`
- `GET /api/v1/sales-orders/search`
- `GET /api/v1/customers/{customerNumber}/account-summary` requiring `finance`
- `GET /api/v1/vendors/search`
- `GET /api/v1/vendors/{vendorNumber}`
- `GET /api/v1/purchase-orders/search`
- `GET /api/v1/purchase-orders/quotes/search`
- `GET /api/v1/purchase-orders/{purchaseOrderNumber}`
- `GET /api/v1/reference/{type}`

Existing sales-order creation now requires the `create` scope. Existing customer, item-validation, and sales-order detail reads require the `read` scope.

Sensitive areas intentionally still excluded:

- Payroll, HR, employee, and personnel data.
- Credit cards, payment vaults, Nuvei/Paya operations, and card preauthorization.
- ACH, check printing, bank account data, and banking writes.
- GL journal creation, AP payments, AR cash receipts, and payment posting.
- Security administration, company/module option writes, deletes, and arbitrary BOI method calls.

## Official Sage Integration Surfaces

### Sage 100 2025 Documentation Baseline

Use the Sage 100 2025 File Layouts and Object Reference as the source of truth for field names, object names, and service methods. The core object choices from the original research are still present in 2025:

- Sales orders: `SO_SalesOrder_Bus`
- Item master: `CI_ItemCode_bus`
- Item/warehouse availability: `IM_ItemWarehouse_svc`
- Customers: `AR_Customer_bus` / `AR_Customer_svc`
- Vendors: `AP_Vendor_bus` / `AP_Vendor_svc`
- Purchase orders: `PO_PurchaseOrder_Bus` / `PO_PurchaseOrder_svc`

The 2025 object pages also show audit-tracking properties on key maintenance/entry objects such as sales orders and item maintenance. That reinforces the recommendation to implement explicit, audited write endpoints rather than generic BOI method invocation.

### BOI / ProvideX COM

The existing middleware uses Sage 100 Business Object Interface through `ProvideX.Script` and `SY_Session`. This is the correct surface for broad Sage 100 functionality because it uses the same business object layer as the Sage desktop application.

Important implementation constraint: many entry objects require setting the proper UI/program context before creating the business object. The current code already follows this for sales orders with `SO_SalesOrder_ui`.

### eBusiness Web Services

Sage 100 eBusiness Web Services is SOAP/WSDL based and platform independent. It is useful as a reference surface but is much narrower than BOI. The documented operation set remains centered on:

- Diagnostics/contract: `GetContractInformation`, `GetDiagnosticInformation`
- Sales order: `GetNextSalesOrderNo`, `GetSalesOrder`, `GetSalesOrderTemplate`, `PreviewSalesOrder`, `CreateSalesOrder`, `UpdateSalesOrder`, `DeleteSalesOrder`
- Customer: `GetCustomer`, `GetNextCustomerNo`, `CreateCustomer`, `UpdateCustomer`, `DeleteCustomer`
- Customer contacts: `GetCustomerContact`, `CreateCustomerContact`, `UpdateCustomerContact`, `DeleteCustomerContact`
- Credit cards: `AddCreditCardToVault`, `PreAuthorizeCreditCard`

The SOAP layer does not cover the wider Sage object universe needed for AI questions such as item availability, vendor purchase history, AR aging, AP open invoices, GL balances, PO receipt history, or production work tickets.

### Sage 100 2025 Changes That Affect Scope

Sage 100 2025 does not reduce the need for the middleware/MCP approach. It makes it more important:

- SData Security and the Native SData Provider Adapter are retired in 2025. Do not design the AI integration around SData.
- Microsoft 365 Connector and Office 365 Contacts are retired. Do not plan customer/contact workflows around those add-ons.
- Inventory Management now includes UPC/EAN fields on item maintenance. Item lookup endpoints should include these fields if present.
- Accounts Receivable added last invoice date and amount in customer maintenance/inquiry. Customer account-summary endpoints should return these fields if available.
- Purchase Order added Request for Quote workflows, quote history/reporting behavior, and cancellation-code requirements for deleting orders/quotes. PO endpoints should support quote/order type distinctions and avoid raw delete tools.
- Production Management added work-ticket and template changes, including rate-per-piece labor fields, Qty Completed, template revision/description behavior, WIP relief, and enhanced scheduling budget behavior. PM endpoints should be version-aware if UAF uses Production Management.
- Nuvei/Paya vault-only account behavior changed. Avoid credit-card MCP tools initially; payment-card operations need separate compliance review.

## Missing Middleware Capabilities Worth Adding

### Priority 1 - Best ROI for AI Queries

These should be added before any broad write capability.

| Capability | Proposed REST endpoints | Sage objects to evaluate | AI/MCP value |
| --- | --- | --- | --- |
| Item master lookup | `GET /api/v1/items/{itemCode}`, `GET /api/v1/items/search` | `CI_ItemCode_bus`, `CI_ItemCode_svc` | Answer "what is this SKU?", description, product line, UOM, status, item type. |
| Real inventory availability | `GET /api/v1/items/{itemCode}/availability`, `POST /api/v1/items/availability` | `IM_ItemWarehouse_svc`, `IM_ItemWarehouse_bus`, `IM_Warehouse_svc` | Quantity on hand, available, by warehouse. Fixes current pass-through inventory validation. |
| Alias/alternate item lookup | `GET /api/v1/items/{itemCode}/aliases`, `GET /api/v1/items/{itemCode}/alternates` | `IM_AliasItem_svc`, `IM_AlternateItem_svc` | Helps AI resolve customer/vendor SKU mismatches. |
| Item pricing context | `GET /api/v1/items/{itemCode}/pricing?customerNumber=...` | `IM_PriceCode_svc`, `AR_PriceLevelByCust_svc`, SO line pricing methods | Lets AI explain expected price and PO mismatch reasons. |
| Sales order search/list | `GET /api/v1/sales-orders/search?customerNumber=&poNumber=&dateFrom=&status=` | `SO_SalesOrder_svc`, `SO_SalesOrderHistory_Svc`, `SO_SalesOrderHistoryInquiry_bus` | Lets AI answer "did this PO already get entered?" |
| Customer account snapshot | `GET /api/v1/customers/{customerNumber}/account-summary` | `AR_OpenInvoice_Svc`, `AR_CustomerSalesHistory_svc`, `AR_Customer_bus` | Aging, balance, credit hold/limit/status, open invoices. |
| Invoice/history lookup | `GET /api/v1/ar/invoices/{invoiceNo}`, `GET /api/v1/customers/{customerNumber}/invoices` | `AR_InvoiceHistoryInquiry_svc`, `AR_OpenInvoice_Svc`, `SO_Invoice_svc` | Common AI accounting questions. |

### Priority 2 - Operational Workflow Expansion

| Capability | Proposed REST endpoints | Sage objects to evaluate | Notes |
| --- | --- | --- | --- |
| Sales order update/cancel | `PATCH /api/v1/sales-orders/{salesOrderNumber}`, `POST /api/v1/sales-orders/{salesOrderNumber}/cancel` | `SO_SalesOrder_bus`, `SO_CancelReasonCode_svc` | Use explicit action endpoints. Avoid generic delete in MCP. |
| Sales order quote/order history | `GET /api/v1/sales-orders/{salesOrderNumber}/history`, `GET /api/v1/customers/{customerNumber}/sales-history` | `SO_SalesOrderHistory_Svc`, `SO_CustomerLastPurchase_svc`, `SO_SalesHistory_bus` | Useful for "what did they buy last time?" |
| SO invoice/shipment data | `GET /api/v1/sales-orders/{salesOrderNumber}/invoices`, `GET /api/v1/invoices/{invoiceNo}/tracking` | `SO_Invoice_svc`, `SO_InvoiceTracking_svc`, `SO_PackageTrackingByItem_svc` | Useful for customer-service questions. |
| Purchase orders | `GET/POST /api/v1/purchase-orders`, `GET /api/v1/purchase-orders/{purchaseOrderNo}` | `PO_PurchaseOrder_Bus`, `PO_PurchaseOrder_svc`, `PO_PurchaseOrderHistoryInq_svc` | Adds purchasing workflow and vendor-facing queries. |
| Purchase order RFQ/quotes | `GET /api/v1/purchase-orders/quotes/search`, `GET /api/v1/purchase-orders/quotes/{quoteNo}`, `POST /api/v1/purchase-orders/quotes/{quoteNo}/generate-order` | `PO_PurchaseOrder_Bus`, `PO_PurchaseOrderHistoryInq_svc`, 2025 RFQ-related PO objects/tasks | New in Sage 100 2025. Start read-only; generating orders from quotes is a controlled write. |
| PO receipts/returns | `GET /api/v1/purchase-orders/{purchaseOrderNo}/receipts`, `POST /api/v1/po-receipts` | `PO_Receipt_Bus`, `PO_ReceiptHistory_svc`, `PO_Return_Bus` | Higher risk writes; start read-only. |
| Vendor master and history | `GET /api/v1/vendors/search`, `GET /api/v1/vendors/{vendorNumber}`, `GET /api/v1/vendors/{vendorNumber}/history` | `AP_Vendor_svc`, `AP_Vendor_bus`, `AP_VendorPurchasesHistory_svc` | Lets AI answer supplier and purchasing questions. |
| AP invoices/open payables | `GET /api/v1/ap/open-invoices`, `GET /api/v1/vendors/{vendorNumber}/invoices` | `AP_OpenInvoice_svc`, `AP_InvoiceHistoryHeader_svc` | Useful for finance team queries. |

### Priority 3 - Finance, Manufacturing, and Admin

| Capability | Proposed REST endpoints | Sage objects to evaluate | Notes |
| --- | --- | --- | --- |
| GL account lookup | `GET /api/v1/gl/accounts/search`, `GET /api/v1/gl/accounts/{accountKey}` | `GL_Account_svc`, `GL_MainAccount_svc`, `GL_AccountGroup_svc` | Read-only first. |
| GL journals | `GET /api/v1/gl/journals/search`, `POST /api/v1/gl/journals` | `GL_GeneralJournal_Bus`, `GL_TransactionJournal_Bus` | Write access is high-risk. |
| Bank reconciliation summary | `GET /api/v1/bank-reconciliation/transactions` | `BR_Transaction_bus`, `BR_Options_Svc` | Read-only. |
| Production Management work tickets | `GET/POST /api/v1/work-tickets` | `PM_WorkTicket_bus`, `PM_WorkTicket_svc`, `PM_WorkTicketHistory_svc` | Relevant only if UAF uses Sage Production Management. |
| Bill of Materials | `GET /api/v1/bills/{billNo}`, `GET /api/v1/items/{itemCode}/where-used` | `BM_Bill_bus`, `BM_BillHeader_svc`, `BM_BillWhereUsed_bus` | Useful for product/manufacturing questions. |
| Return Merchandise Authorization | `GET/POST /api/v1/rma/receipts`, `GET/POST /api/v1/rma/returns` | `RA_Receipts_Bus`, `RA_Return_Bus` | Add if returns workflow matters. |
| Options/reference data | `GET /api/v1/reference/{type}` | `*_Options_Svc`, terms, divisions, tax schedules, warehouses; validate Ship Via object on host before adding | Needed to help AI validate user requests and produce structured forms. |

## "All Possible Functions" Strategy

Do not expose all BOI objects directly as bespoke endpoints. The object surface includes hundreds of report/UI/update/helper classes that are not stable or appropriate as public API.

Instead, implement three layers:

1. Curated REST resources for common business workflows.
2. A read-only generic query layer for service objects and table-backed lookups:
   - `POST /api/v1/query/sage-service`
   - Inputs: module, serviceObject, filters, fields, limit, cursor.
   - Allowlist only `_svc` objects and fields.
   - No arbitrary method invocation.
3. Admin-only object catalog:
   - `GET /api/v1/sage/catalog/modules`
   - `GET /api/v1/sage/catalog/objects?module=SO&type=svc`
   - `GET /api/v1/sage/catalog/objects/{objectName}/schema`
   - Built from an allowlist plus runtime `GetColumns$`/`GetKeyColumns$` where supported.

This gives AI broad read coverage without letting an LLM call arbitrary BOI methods.

## MCP Server Recommendation

Build a separate MCP server that calls the middleware over HTTPS. Do not run ProvideX/COM inside the MCP process.

Recommended MCP tools:

### Safe Read Tools

- `sage_health_check`
- `sage_search_customers`
- `sage_get_customer`
- `sage_resolve_customer`
- `sage_search_items`
- `sage_get_item`
- `sage_get_item_availability`
- `sage_get_item_alternates`
- `sage_search_sales_orders`
- `sage_get_sales_order`
- `sage_get_customer_account_summary`
- `sage_get_customer_sales_history`
- `sage_search_invoices`
- `sage_get_invoice`
- `sage_search_vendors`
- `sage_get_vendor`
- `sage_search_purchase_orders`
- `sage_get_purchase_order`
- `sage_search_purchase_order_quotes`
- `sage_query_reference_data`

### Controlled Write Tools

Start with only:

- `sage_create_sales_order`
- `sage_preview_sales_order`

Add later behind approval/audit:

- `sage_update_sales_order`
- `sage_cancel_sales_order`
- `sage_create_purchase_order`
- `sage_generate_purchase_order_from_quote`
- `sage_create_ap_invoice`
- `sage_create_gl_journal`

### MCP Safety Requirements

- Read-only default mode.
- Separate API keys/scopes for read, create, modify, finance, and admin.
- Per-tool confirmation for mutating operations.
- Idempotency keys for creates.
- Full audit trail: user, model/client, tool name, arguments hash, Sage company, result, and sales/order/invoice numbers.
- Redaction for PII, payment details, and credentials.
- Response shaping: small, summarized defaults with an option to include line details.
- Company/profile scoping so ChatGPT/Claude cannot accidentally query the wrong Sage company.

## Implementation Notes

### BOI Access

- Continue using a pooled `SY_Session` design.
- Add a shared BOI helper for common service-object operations:
  - set program context when needed,
  - create object,
  - set keys/filters,
  - read fields,
  - iterate with scan limits,
  - release COM objects reliably,
  - convert Sage errors into structured API errors.
- Build object-specific services around that helper rather than repeating dynamic COM code in controllers.

### Inventory Fix

The current inventory service is pass-through. Replace it with:

- `CI_ItemCode_bus` or `CI_ItemCode_svc` for item existence and item metadata.
- `IM_ItemWarehouse_svc` for quantity available/on-hand by warehouse.
- `IM_AliasItem_svc` and `IM_AlternateItem_svc` for customer/vendor SKU resolution.
- Include Sage 100 2025 UPC/EAN fields in item DTOs when available.

### Query Limits

Many current reads scan records with hard caps. For AI/MCP use, every list/search endpoint should require:

- limit with conservative max,
- indexed key or filter where possible,
- timeout/cancellation,
- stable pagination/cursor,
- explicit field selection for generic queries.

### Writes

Write endpoints should be explicit workflow commands. Avoid `POST /boi/call-method`. For every write:

- support dry-run/preview where possible,
- validate reference data first,
- use idempotency keys,
- return Sage-confirmed document numbers and line values,
- record audit logs.

## Proposed Build Sequence

1. Add item master and real availability endpoints.
2. Add sales-order search and duplicate PO lookup.
3. Add customer account summary: open invoices, balance/aging, sales history.
4. Add invoice lookup, sales history, and 2025 last-invoice date/amount fields.
5. Add vendor and purchase-order read endpoints, including 2025 RFQ/quote awareness.
6. Add read-only generic service query with strict allowlist.
7. Build MCP server with read tools plus `sage_create_sales_order`.
8. Add controlled write tools only after field testing and client approval.

## Primary Sources

- Sage 100 2025 File Layouts and Object Reference documentation: https://help-sage100.na.sage.com/2025/FLOR/index.htm
- Sage 100 2025 What's New / Upgrade Guide: https://help-sage100.na.sage.com/UpgradeGuide/2025/Content/UpgradeGuide/WhatsNew_2025.htm
- Sage 100 2022 eBusiness Web Services Guide: https://docs.sage.com/docs/en/customer/100erp/2022/open/WebServices.pdf
- Sage 100 2023 `SO_SalesOrder_Bus` object reference: https://help-sage100.na.sage.com/2023/FLOR/Content/Object_Reference/SO/SO_SalesOrder_Bus.html
- Sage 100 2025 `SO_SalesOrder_Bus` object reference: https://help-sage100.na.sage.com/2025/FLOR/Content/Object_Reference/SO/SO_SalesOrder_Bus.html
- Sage 100 2025 `CI_ItemCode_bus` object reference: https://help-sage100.na.sage.com/2025/FLOR/Content/Object_Reference/CI/CI_ItemCode_bus.html
- Sage 100 2025 `IM_ItemWarehouse_svc` object reference: https://help-sage100.na.sage.com/2025/FLOR/Content/Object_Reference/IM/IM_ItemWarehouse_svc.html
- Sage 100 2020 `CI_ItemCode_bus` object reference: https://help-sage100.na.sage.com/2020/FLOR/Content/Object_Reference/CI/CI_ItemCode_bus.html
- Sage 100 2020 `IM_ItemWarehouse_svc` object reference: https://help-sage100.na.sage.com/2020/FLOR/Content/Object_Reference/IM/IM_ItemWarehouse_svc.html
- Sage 100 2025 object-reference/program-listing pages inspected directly with the same URL patterns, including `AR_Customer_bus`, `AP_Vendor_bus`, `PO_PurchaseOrder_Bus`, and `GL_Account_bus`.
