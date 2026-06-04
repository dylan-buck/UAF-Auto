import test from "node:test";
import assert from "node:assert/strict";
import { loadConfig } from "../src/config.js";
import { enabledToolNames, sageToolDefinitions } from "../src/tools.js";

test("default MCP surface excludes finance and create tools", () => {
  const config = loadConfig({ UAF_SAGE_READ_API_KEY: "read" });
  const names = enabledToolNames(config);

  assert(names.includes("sage_search_items"));
  assert(names.includes("sage_search_customers"));
  assert(!names.includes("sage_get_customer_account_summary"));
  assert(!names.includes("sage_create_sales_order"));
});

test("read tools are annotated as read-only", () => {
  for (const tool of sageToolDefinitions.filter((definition) => definition.name !== "sage_create_sales_order")) {
    assert.equal(tool.annotations.readOnlyHint, true, `${tool.name} should be read-only`);
  }

  const createTool = sageToolDefinitions.find((tool) => tool.name === "sage_create_sales_order");
  assert(createTool);
  assert.equal(createTool.annotations.readOnlyHint, false);
  assert.equal(createTool.annotations.idempotentHint, false);
});
