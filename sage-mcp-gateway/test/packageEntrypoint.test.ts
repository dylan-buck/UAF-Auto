import test from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import packageJson from "../package.json" with { type: "json" };

test("package entrypoint matches the TypeScript build output", () => {
  assert.equal(packageJson.main, "dist/src/index.js");
  assert.equal(packageJson.scripts.start, "node dist/src/index.js");
  assert.equal(existsSync(packageJson.main), true);
});
