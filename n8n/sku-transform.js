"use strict";

/**
 * UAF SKU transformation rules.
 *
 * Business rules:
 * 1) If description contains "Poly" (case-insensitive), keep full SKU.
 * 2) Else if description contains "Merv 10" (case-insensitive), drop leading "FT"
 *    only when the SKU starts with "FT".
 * 3) Otherwise, leave SKU unchanged.
 */

function normalizeText(value) {
  return String(value || "").toLowerCase();
}

function trimSku(value) {
  return String(value || "").trim();
}

function startsWithFt(sku) {
  return sku.toUpperCase().startsWith("FT");
}

function transformSkuByDescription(itemCode, description) {
  const original = trimSku(itemCode);
  const descriptionNorm = normalizeText(description);

  const hasPoly = descriptionNorm.includes("poly");
  const hasMerv10 = descriptionNorm.includes("merv 10");

  if (hasPoly) {
    return {
      originalItemCode: original,
      transformedItemCode: original,
      transformRuleApplied: "POLY_KEEP_FULL_SKU",
    };
  }

  if (hasMerv10) {
    if (startsWithFt(original)) {
      return {
        originalItemCode: original,
        transformedItemCode: original.slice(2),
        transformRuleApplied: "MERV10_DROP_LEADING_FT",
      };
    }

    return {
      originalItemCode: original,
      transformedItemCode: original,
      transformRuleApplied: "MERV10_NO_FT_PREFIX_NO_CHANGE",
    };
  }

  return {
    originalItemCode: original,
    transformedItemCode: original,
    transformRuleApplied: "NONE",
  };
}

function transformLineItem(lineItem) {
  const item = lineItem || {};
  const result = transformSkuByDescription(item.itemCode, item.description);

  return {
    ...item,
    itemCode: result.transformedItemCode,
    skuTransform: result,
  };
}

function transformLineItems(lineItems) {
  const source = Array.isArray(lineItems) ? lineItems : [];
  return source.map(transformLineItem);
}

module.exports = {
  transformSkuByDescription,
  transformLineItem,
  transformLineItems,
};

