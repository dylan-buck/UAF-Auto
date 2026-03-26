"use strict";

/**
 * n8n Code node snippet.
 *
 * Usage:
 * - Place this logic in a Code node after extraction and before middleware calls.
 * - Input item JSON is expected to include:
 *   - lineItems: [{ itemCode, description, ... }]
 */

const ENABLE_UAF_SKU_TRANSFORM = true;
const INCLUDE_AUDIT_FIELDS = true;

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
    return {
      originalItemCode,
      transformedItemCode: originalItemCode,
      transformRuleApplied: "POLY_KEEP_FULL_SKU",
    };
  }

  if (hasMerv10) {
    if (startsWithFt(originalItemCode)) {
      return {
        originalItemCode,
        transformedItemCode: originalItemCode.slice(2),
        transformRuleApplied: "MERV10_DROP_LEADING_FT",
      };
    }

    return {
      originalItemCode,
      transformedItemCode: originalItemCode,
      transformRuleApplied: "MERV10_NO_FT_PREFIX_NO_CHANGE",
    };
  }

  return {
    originalItemCode,
    transformedItemCode: originalItemCode,
    transformRuleApplied: "NONE",
  };
}

return $input.all().map((item) => {
  const json = item.json || {};
  const lineItems = Array.isArray(json.lineItems) ? json.lineItems : [];

  if (!ENABLE_UAF_SKU_TRANSFORM) {
    return { json };
  }

  const transformedLineItems = lineItems.map((line) => {
    const transform = transformSku(line.itemCode, line.description);
    const lineOut = {
      ...line,
      itemCode: transform.transformedItemCode,
    };

    if (INCLUDE_AUDIT_FIELDS) {
      lineOut.skuTransform = transform;
    }

    return lineOut;
  });

  return {
    json: {
      ...json,
      lineItems: transformedLineItems,
    },
  };
});

