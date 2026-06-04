import test from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { UafApiError, UafClient } from "../src/uafClient.js";

test("UafClient sends scoped API key and query values", async () => {
  const seen: Array<{ url?: string; key?: string; method?: string }> = [];
  const server = createServer((req, res) => {
    seen.push({ url: req.url, key: req.headers["x-api-key"] as string, method: req.method });
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ ok: true }));
  });

  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  assert(address && typeof address === "object");

  const client = new UafClient({
    baseUrl: `http://127.0.0.1:${address.port}`,
    timeoutMs: 1000,
    keys: { read: "read-key" }
  });

  try {
    assert.deepEqual(await client.searchCustomers({ name: "Acme", limit: 5, phone: "" }), { ok: true });
    assert.equal(seen[0].method, "GET");
    assert.equal(seen[0].url, "/api/v1/customers/search?name=Acme&limit=5");
    assert.equal(seen[0].key, "read-key");
  } finally {
    server.close();
  }
});

test("UafClient reports upstream errors with status and parsed details", async () => {
  const server = createServer((_req, res) => {
    res.statusCode = 503;
    res.setHeader("content-type", "application/json");
    res.end(JSON.stringify({ error: "busy" }));
  });

  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  assert(address && typeof address === "object");

  const client = new UafClient({
    baseUrl: `http://127.0.0.1:${address.port}`,
    timeoutMs: 1000,
    keys: { read: "read-key" }
  });

  try {
    await assert.rejects(() => client.readiness(), (error) => {
      assert(error instanceof UafApiError);
      assert.equal(error.status, 503);
      assert.deepEqual(error.details, { error: "busy" });
      return true;
    });
  } finally {
    server.close();
  }
});
