import test from "node:test";
import assert from "node:assert/strict";
import { assertRunnableConfig, enabledForTest, loadConfig } from "./helpers.js";

test("loadConfig defaults to localhost, read-only tools, and UAF middleware on localhost", () => {
  const config = loadConfig({
    UAF_SAGE_READ_API_KEY: "read-secret"
  });

  assert.equal(config.host, "127.0.0.1");
  assert.equal(config.port, 8787);
  assert.equal(config.sageApiUrl, "http://localhost:3000");
  assert.equal(config.keys.read, "read-secret");
  assert.equal(config.enabled.createTools, false);
  assert.equal(config.enabled.financeTools, false);
});

test("loadConfig supports enterprise env aliases and optional create/finance tool gates", () => {
  const config = loadConfig({
    UAF_BASE_URL: "http://localhost:3001",
    UAF_API_KEY_READ: "read",
    UAF_API_KEY_CREATE: "create",
    UAF_API_KEY_FINANCE: "finance",
    MCP_HOST: "localhost",
    MCP_PORT: "8788",
    ENABLE_CREATE_TOOLS: "true",
    ENABLE_FINANCE_TOOLS: "1"
  });

  assert.equal(config.host, "localhost");
  assert.equal(config.port, 8788);
  assert.equal(config.sageApiUrl, "http://localhost:3001");
  assert.deepEqual(config.keys, { read: "read", create: "create", finance: "finance" });
  assert.deepEqual(enabledForTest(config).slice(-2), [
    "sage_get_customer_account_summary",
    "sage_create_sales_order"
  ]);
});

test("assertRunnableConfig blocks missing read key and missing gated keys", () => {
  assert.throws(() => assertRunnableConfig(loadConfig({})), /Missing UAF_SAGE_READ_API_KEY/);
  assert.throws(
    () =>
      assertRunnableConfig(
        loadConfig({
          UAF_SAGE_READ_API_KEY: "read",
          ENABLE_FINANCE_TOOLS: "true",
          UAF_SAGE_FINANCE_API_KEY: ""
        })
      ),
    /requires UAF_SAGE_FINANCE_API_KEY/
  );
});
