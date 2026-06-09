import test from "node:test";
import assert from "node:assert/strict";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";
import { createGatewayApp } from "../src/server.js";
import { loadConfig } from "../src/config.js";

test("gateway health and bearer auth work", async () => {
  const config = loadConfig({ UAF_SAGE_READ_API_KEY: "read", MCP_SHARED_SECRET: "secret" });
  const app = createGatewayApp(config, fakeClient());
  const server = app.listen(0, "127.0.0.1");

  await onceListening(server);
  const address = server.address();
  assert(address && typeof address === "object");
  const baseUrl = `http://127.0.0.1:${address.port}`;

  try {
    const health = await fetch(`${baseUrl}/healthz`);
    assert.equal(health.status, 200);
    assert.deepEqual(await health.json(), { ok: true, service: "sage-mcp-gateway" });

    const unauthorized = await fetch(`${baseUrl}/mcp`, { method: "POST" });
    assert.equal(unauthorized.status, 401);
  } finally {
    server.close();
  }
});

test("gateway rejects disallowed browser origins", async () => {
  const config = loadConfig({
    UAF_SAGE_READ_API_KEY: "read",
    MCP_ALLOWED_ORIGINS: "https://mcp.example.com"
  });
  const app = createGatewayApp(config, fakeClient());
  const server = app.listen(0, "127.0.0.1");

  await onceListening(server);
  const address = server.address();
  assert(address && typeof address === "object");

  try {
    const blocked = await fetch(`http://127.0.0.1:${address.port}/healthz`, {
      headers: { Origin: "https://evil.example.com" }
    });
    assert.equal(blocked.status, 403);

    const allowed = await fetch(`http://127.0.0.1:${address.port}/healthz`, {
      headers: { Origin: "https://mcp.example.com" }
    });
    assert.equal(allowed.status, 200);
  } finally {
    server.close();
  }
});

test("gateway exposes enabled tools over real Streamable HTTP MCP", async () => {
  const config = loadConfig({
    UAF_SAGE_READ_API_KEY: "read",
    ENABLE_FINANCE_TOOLS: "true",
    UAF_SAGE_FINANCE_API_KEY: "finance"
  });
  const app = createGatewayApp(config, fakeClient());
  const server = app.listen(0, "127.0.0.1");

  await onceListening(server);
  const address = server.address();
  assert(address && typeof address === "object");

  const client = new Client({ name: "test-client", version: "0.0.0" });
  const transport = new StreamableHTTPClientTransport(new URL(`http://127.0.0.1:${address.port}/mcp`));

  try {
    await client.connect(transport);
    const tools = await client.listTools();
    const names = tools.tools.map((tool) => tool.name);
    assert(names.includes("sage_search_customers"));
    assert(names.includes("sage_get_customer_account_summary"));
    assert(!names.includes("sage_create_sales_order"));
  } finally {
    await client.close();
    server.close();
  }
});

function fakeClient() {
  return {
    readiness: async () => ({ status: "ready" }),
    searchItems: async () => ({}),
    getItem: async () => ({}),
    getItemAvailability: async () => ({}),
    getBulkAvailability: async () => ({}),
    getItemAliases: async () => ({}),
    getItemAlternates: async () => ({}),
    validateItems: async () => ({}),
    checkItemExists: async () => ({}),
    searchCustomers: async () => ({}),
    getCustomer: async () => ({}),
    validateShipTo: async () => ({}),
    resolveCustomer: async () => ({}),
    getCustomerAccountSummary: async () => ({}),
    searchSalesOrders: async () => ({}),
    getSalesOrderDetails: async () => ({}),
    createSalesOrder: async () => ({}),
    searchVendors: async () => ({}),
    getVendor: async () => ({}),
    searchPurchaseOrders: async () => ({}),
    searchPurchaseOrderQuotes: async () => ({}),
    getPurchaseOrder: async () => ({}),
    getReferenceData: async () => ({})
  } as never;
}

async function onceListening(server: ReturnType<typeof import("node:http").createServer>): Promise<void> {
  if (server.listening) {
    return;
  }

  await new Promise<void>((resolve) => server.once("listening", resolve));
}
