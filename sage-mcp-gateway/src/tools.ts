import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod/v4";
import { GatewayConfig } from "./config.js";
import { Query, UafApiError, UafClient } from "./uafClient.js";

type ToolAnnotations = {
  title?: string;
  readOnlyHint?: boolean;
  destructiveHint?: boolean;
  idempotentHint?: boolean;
  openWorldHint?: boolean;
};

export interface SageToolDefinition {
  name: string;
  title: string;
  description: string;
  inputSchema: Record<string, z.ZodType>;
  annotations: ToolAnnotations;
  enabled: (config: GatewayConfig) => boolean;
  handler: (client: UafClient, input: Record<string, unknown>) => Promise<unknown>;
}

const optionalString = z.string().min(1).optional();
const optionalLimit = z.number().int().min(1).max(100).optional();

export const sageToolDefinitions: SageToolDefinition[] = [
  {
    name: "sage_health_check",
    title: "Sage Health Check",
    description: "Check whether the UAF Sage middleware and Sage 100 connection are ready.",
    inputSchema: {},
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client) => client.readiness()
  },
  {
    name: "sage_search_items",
    title: "Search Sage Items",
    description: "Search Sage inventory items by text query and optional product line.",
    inputSchema: { q: optionalString, productLine: optionalString, limit: optionalLimit },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchItems(input as Query)
  },
  {
    name: "sage_get_item",
    title: "Get Sage Item",
    description: "Get Sage item master details for a single item code.",
    inputSchema: { itemCode: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getItem(input.itemCode as string)
  },
  {
    name: "sage_get_item_availability",
    title: "Get Sage Item Availability",
    description: "Get warehouse availability for a single Sage item.",
    inputSchema: { itemCode: z.string().min(1), warehouseCode: optionalString },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getItemAvailability(input as { itemCode: string; warehouseCode?: string })
  },
  {
    name: "sage_get_bulk_item_availability",
    title: "Get Bulk Sage Item Availability",
    description: "Get availability for multiple Sage items.",
    inputSchema: {
      items: z.array(z.object({ itemCode: z.string().min(1), warehouseCode: optionalString })).min(1).max(100)
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getBulkAvailability(input)
  },
  {
    name: "sage_get_item_aliases",
    title: "Get Sage Item Aliases",
    description: "Get Sage alias item codes for a given item.",
    inputSchema: { itemCode: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getItemAliases(input.itemCode as string)
  },
  {
    name: "sage_get_item_alternates",
    title: "Get Sage Item Alternates",
    description: "Get Sage alternate/substitute item codes for a given item.",
    inputSchema: { itemCode: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getItemAlternates(input.itemCode as string)
  },
  {
    name: "sage_validate_items",
    title: "Validate Sage Items",
    description: "Validate that a list of item codes exists in Sage 100.",
    inputSchema: { itemCodes: z.array(z.string().min(1)).min(1).max(100) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.validateItems(input)
  },
  {
    name: "sage_check_item_exists",
    title: "Check Sage Item Exists",
    description: "Check whether a single item code exists in Sage 100.",
    inputSchema: { itemCode: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.checkItemExists(input.itemCode as string)
  },
  {
    name: "sage_search_customers",
    title: "Search Sage Customers",
    description: "Search Sage customers by name, address, city, state, or phone.",
    inputSchema: {
      name: optionalString,
      address: optionalString,
      city: optionalString,
      state: optionalString,
      phone: optionalString,
      limit: optionalLimit
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchCustomers(input as Query)
  },
  {
    name: "sage_get_customer",
    title: "Get Sage Customer",
    description: "Get Sage customer master details by customer number.",
    inputSchema: { customerNumber: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getCustomer(input.customerNumber as string)
  },
  {
    name: "sage_validate_ship_to",
    title: "Validate Sage Ship-To",
    description: "Validate a ship-to address against a Sage customer.",
    inputSchema: {
      customerNumber: z.string().min(1),
      addressLine1: optionalString,
      addressLine2: optionalString,
      city: optionalString,
      state: optionalString,
      zipCode: optionalString
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => {
      const { customerNumber, ...body } = input;
      return client.validateShipTo(customerNumber as string, body);
    }
  },
  {
    name: "sage_resolve_customer",
    title: "Resolve Sage Customer",
    description: "Find the best Sage customer/ship-to match from PO/customer data.",
    inputSchema: {
      customerName: z.string().min(1),
      billToAddress: z.record(z.string(), z.unknown()).optional(),
      shipToAddress: z.record(z.string(), z.unknown()).optional()
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.resolveCustomer(input)
  },
  {
    name: "sage_search_sales_orders",
    title: "Search Sage Sales Orders",
    description: "Search Sage sales orders by customer, PO number, date, or status.",
    inputSchema: {
      customerNumber: optionalString,
      poNumber: optionalString,
      dateFrom: optionalString,
      status: optionalString,
      limit: optionalLimit
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchSalesOrders(input as Query)
  },
  {
    name: "sage_get_sales_order",
    title: "Get Sage Sales Order",
    description: "Get Sage-confirmed details for an existing sales order.",
    inputSchema: { salesOrderNumber: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getSalesOrderDetails(input.salesOrderNumber as string)
  },
  {
    name: "sage_search_vendors",
    title: "Search Sage Vendors",
    description: "Search Sage vendors by text, city, or state.",
    inputSchema: { q: optionalString, city: optionalString, state: optionalString, limit: optionalLimit },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchVendors(input as Query)
  },
  {
    name: "sage_get_vendor",
    title: "Get Sage Vendor",
    description: "Get Sage vendor master details by vendor number.",
    inputSchema: { vendorNumber: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getVendor(input.vendorNumber as string)
  },
  {
    name: "sage_search_purchase_orders",
    title: "Search Sage Purchase Orders",
    description: "Search Sage purchase orders by vendor, order type, status, or date.",
    inputSchema: {
      vendorNumber: optionalString,
      orderType: optionalString,
      status: optionalString,
      dateFrom: optionalString,
      limit: optionalLimit
    },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchPurchaseOrders(input as Query)
  },
  {
    name: "sage_search_purchase_order_quotes",
    title: "Search Sage Purchase Order Quotes",
    description: "Search Sage purchase order quotes.",
    inputSchema: { vendorNumber: optionalString, status: optionalString, dateFrom: optionalString, limit: optionalLimit },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.searchPurchaseOrderQuotes(input as Query)
  },
  {
    name: "sage_get_purchase_order",
    title: "Get Sage Purchase Order",
    description: "Get Sage purchase order details by purchase order number.",
    inputSchema: { purchaseOrderNumber: z.string().min(1) },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getPurchaseOrder(input.purchaseOrderNumber as string)
  },
  {
    name: "sage_get_reference_data",
    title: "Get Sage Reference Data",
    description: "Get configured Sage reference data such as warehouses, terms, ship methods, or product lines.",
    inputSchema: { type: z.string().min(1), limit: optionalLimit },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: () => true,
    handler: (client, input) => client.getReferenceData(input.type as string, input.limit as number | undefined)
  },
  {
    name: "sage_get_customer_account_summary",
    title: "Get Sage Customer Account Summary",
    description: "Get finance-sensitive customer balances and open invoice summary. Requires finance tools to be enabled.",
    inputSchema: { customerNumber: z.string().min(1), openInvoiceLimit: optionalLimit },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true },
    enabled: (config) => config.enabled.financeTools,
    handler: (client, input) =>
      client.getCustomerAccountSummary(input.customerNumber as string, input.openInvoiceLimit as number | undefined)
  },
  {
    name: "sage_create_sales_order",
    title: "Create Sage Sales Order",
    description: "Create a Sage sales order. Non-idempotent; enable only for approved write workflows.",
    inputSchema: {
      customerNumber: z.string().min(1),
      poNumber: z.string().min(1),
      orderDate: optionalString,
      shipToAddress: z.record(z.string(), z.unknown()).optional(),
      lines: z.array(z.record(z.string(), z.unknown())).min(1).max(100)
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false },
    enabled: (config) => config.enabled.createTools,
    handler: (client, input) => client.createSalesOrder(input)
  }
];

export function enabledToolNames(config: GatewayConfig): string[] {
  return sageToolDefinitions.filter((tool) => tool.enabled(config)).map((tool) => tool.name);
}

export function registerSageTools(server: McpServer, client: UafClient, config: GatewayConfig): void {
  for (const tool of sageToolDefinitions) {
    if (!tool.enabled(config)) {
      continue;
    }

    server.registerTool(
      tool.name,
      {
        title: tool.title,
        description: tool.description,
        inputSchema: tool.inputSchema,
        annotations: tool.annotations
      },
      async (input) => {
        try {
          const result = await tool.handler(client, input as Record<string, unknown>);
          return toMcpResult(result);
        } catch (error) {
          return toMcpError(error);
        }
      }
    );
  }
}

function toMcpResult(result: unknown) {
  return {
    content: [
      {
        type: "text" as const,
        text: JSON.stringify(result, null, 2)
      }
    ]
  };
}

function toMcpError(error: unknown) {
  const detail =
    error instanceof UafApiError
      ? { error: error.message, status: error.status, details: error.details }
      : { error: error instanceof Error ? error.message : "Unknown error" };

  return {
    isError: true,
    content: [
      {
        type: "text" as const,
        text: JSON.stringify(detail, null, 2)
      }
    ]
  };
}
