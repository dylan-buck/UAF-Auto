"use strict";

const assert = require("node:assert/strict");
const {
  transformSkuByDescription,
  transformLineItems,
} = require("./sku-transform");

function run() {
  {
    const result = transformSkuByDescription(
      "FT10101",
      "MERV 10 pleated filter standard capacity"
    );
    assert.equal(result.transformedItemCode, "10101");
    assert.equal(result.transformRuleApplied, "MERV10_DROP_LEADING_FT");
  }

  {
    const result = transformSkuByDescription(
      "FT10101",
      "MERV 10 POLY filter"
    );
    assert.equal(result.transformedItemCode, "FT10101");
    assert.equal(result.transformRuleApplied, "POLY_KEEP_FULL_SKU");
  }

  {
    const result = transformSkuByDescription("FT20202", "Poly media");
    assert.equal(result.transformedItemCode, "FT20202");
    assert.equal(result.transformRuleApplied, "POLY_KEEP_FULL_SKU");
  }

  {
    const result = transformSkuByDescription("10101", "Merv 10");
    assert.equal(result.transformedItemCode, "10101");
    assert.equal(result.transformRuleApplied, "MERV10_NO_FT_PREFIX_NO_CHANGE");
  }

  {
    const result = transformSkuByDescription("FT12121", "MERV 8");
    assert.equal(result.transformedItemCode, "FT12121");
    assert.equal(result.transformRuleApplied, "NONE");
  }

  {
    const result = transformSkuByDescription("  ft15151  ", "MERV 10");
    assert.equal(result.transformedItemCode, "15151");
    assert.equal(result.transformRuleApplied, "MERV10_DROP_LEADING_FT");
  }

  {
    const result = transformSkuByDescription("ABFT16161", "MERV 10");
    assert.equal(result.transformedItemCode, "ABFT16161");
    assert.equal(result.transformRuleApplied, "MERV10_NO_FT_PREFIX_NO_CHANGE");
  }

  {
    const transformed = transformLineItems([
      { itemCode: "FT12345", description: "merv 10" },
      { itemCode: "FT99999", description: "poly media" },
      { itemCode: "12345", description: "merv 10" },
    ]);
    assert.equal(transformed[0].itemCode, "12345");
    assert.equal(transformed[1].itemCode, "FT99999");
    assert.equal(transformed[2].itemCode, "12345");
  }

  console.log("sku-transform tests passed");
}

run();

