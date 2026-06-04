using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class ItemService : SageReadServiceBase, IItemService
{
    private const int MaxScan = 1000;
    private readonly ILogger<ItemService> _logger;

    public ItemService(IProvideXSessionManager sessionManager, ILogger<ItemService> logger)
        : base(sessionManager, logger)
    {
        _logger = logger;
    }

    public Task<ItemDto?> GetItemAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        var normalized = itemCode.Trim();
        return WithSageObjectAsync<ItemDto?>("CI_ItemCode_bus", item =>
        {
            if (!TryFind(item, ("ItemCode$", normalized)))
            {
                return null;
            }

            return ExtractItem(item);
        }, cancellationToken);
    }

    public Task<ItemSearchResponse> SearchItemsAsync(
        string? query,
        string? productLine,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var normalizedQuery = query?.Trim();
        var normalizedProductLine = productLine?.Trim();

        return WithSageObjectAsync("CI_ItemCode_svc", itemSvc =>
        {
            var response = new ItemSearchResponse();
            if (!MoveFirst(itemSvc))
            {
                return response;
            }

            var hasMore = true;
            while (hasMore && response.Items.Count < safeLimit && response.ScannedCount < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.ScannedCount++;

                var item = ExtractItem(itemSvc);
                if (MatchesItem(item, normalizedQuery, normalizedProductLine))
                {
                    response.Items.Add(item);
                }

                hasMore = MoveNext(itemSvc);
            }

            response.TotalCount = response.Items.Count;
            LogScanLimit("CI_ItemCode_svc", response.ScannedCount, MaxScan);
            return response;
        }, cancellationToken);
    }

    public Task<ItemAvailabilityResponse> GetAvailabilityAsync(
        ItemAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedItems = request.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.ItemCode))
            .Select(i => new ItemAvailabilityRequestLine
            {
                ItemCode = i.ItemCode.Trim(),
                WarehouseCode = i.WarehouseCode?.Trim()
            })
            .ToList();

        return WithSageObjectAsync("IM_ItemWarehouse_svc", itemWhseSvc =>
        {
            var response = new ItemAvailabilityResponse();

            foreach (var requested in requestedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var itemResult = new ItemAvailabilityDto { ItemCode = requested.ItemCode };

                if (MoveFirst(itemWhseSvc))
                {
                    var scanned = 0;
                    var hasMore = true;
                    while (hasMore && scanned < MaxScan)
                    {
                        scanned++;
                        var recordItem = GetStringValue(itemWhseSvc, "ItemCode$").Trim();
                        var recordWarehouse = GetStringValue(itemWhseSvc, "WarehouseCode$").Trim();

                        if (recordItem.Equals(requested.ItemCode, StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrWhiteSpace(requested.WarehouseCode) ||
                             recordWarehouse.Equals(requested.WarehouseCode, StringComparison.OrdinalIgnoreCase)))
                        {
                            itemResult.Warehouses.Add(ExtractWarehouseAvailability(itemWhseSvc, recordWarehouse));
                        }

                        hasMore = MoveNext(itemWhseSvc);
                    }

                    LogScanLimit("IM_ItemWarehouse_svc", scanned, MaxScan);
                }

                if (itemResult.Warehouses.Count == 0 && !string.IsNullOrWhiteSpace(requested.WarehouseCode))
                {
                    itemResult.Warehouses.Add(new ItemWarehouseAvailabilityDto
                    {
                        WarehouseCode = requested.WarehouseCode,
                        QuantityAvailable = TryCalcQuantityAvailable(itemWhseSvc, requested.ItemCode, requested.WarehouseCode)
                    });
                }

                response.Items.Add(itemResult);
            }

            return response;
        }, cancellationToken);
    }

    public Task<ItemRelatedItemsResponse> GetAliasesAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        return GetRelatedItemsAsync("IM_AliasItem_svc", "alias", itemCode, cancellationToken);
    }

    public Task<ItemRelatedItemsResponse> GetAlternatesAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        return GetRelatedItemsAsync("IM_AlternateItem_svc", "alternate", itemCode, cancellationToken);
    }

    private Task<ItemRelatedItemsResponse> GetRelatedItemsAsync(
        string objectName,
        string relationshipType,
        string itemCode,
        CancellationToken cancellationToken)
    {
        var normalized = itemCode.Trim();
        return WithSageObjectAsync(objectName, relatedSvc =>
        {
            var response = new ItemRelatedItemsResponse
            {
                ItemCode = normalized,
                RelationshipType = relationshipType
            };

            if (!MoveFirst(relatedSvc))
            {
                return response;
            }

            var scanned = 0;
            var hasMore = true;
            while (hasMore && scanned < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var recordItem = FirstNonEmpty(
                    GetStringValue(relatedSvc, "ItemCode$"),
                    GetStringValue(relatedSvc, "AliasItemNo$"),
                    GetStringValue(relatedSvc, "AlternateItemCode$")).Trim();

                var relatedItem = FirstNonEmpty(
                    GetStringValue(relatedSvc, "AliasItemCode$"),
                    GetStringValue(relatedSvc, "AliasItemNo$"),
                    GetStringValue(relatedSvc, "AlternateItemCode$")).Trim();

                if (recordItem.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                    relatedItem.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    response.Items.Add(new ItemRelatedItemDto
                    {
                        ItemCode = recordItem,
                        RelatedItemCode = relatedItem,
                        Description = FirstNonEmpty(
                            GetStringValue(relatedSvc, "ItemCodeDesc$"),
                            GetStringValue(relatedSvc, "AliasItemDesc$"),
                            GetStringValue(relatedSvc, "AlternateItemDesc$")),
                        RelationshipCode = FirstNonEmpty(
                            GetStringValue(relatedSvc, "AliasType$"),
                            GetStringValue(relatedSvc, "AlternateType$"))
                    });
                }

                hasMore = MoveNext(relatedSvc);
            }

            LogScanLimit(objectName, scanned, MaxScan);
            return response;
        }, cancellationToken);
    }

    private static ItemDto ExtractItem(dynamic item)
    {
        return new ItemDto
        {
            ItemCode = GetStringValue(item, "ItemCode$").Trim(),
            Description = GetStringValue(item, "ItemCodeDesc$"),
            ProductLine = GetStringValue(item, "ProductLine$"),
            ItemType = GetStringValue(item, "ItemType$"),
            StandardUnitOfMeasure = GetStringValue(item, "StandardUnitOfMeasure$"),
            SalesUnitOfMeasure = GetStringValue(item, "SalesUnitOfMeasure$"),
            PurchaseUnitOfMeasure = GetStringValue(item, "PurchaseUnitOfMeasure$"),
            UpcCode = FirstNonEmpty(GetStringValue(item, "UPCCode$"), GetStringValue(item, "UPC$")),
            EanCode = FirstNonEmpty(GetStringValue(item, "EANCode$"), GetStringValue(item, "EAN$")),
            Inactive = GetBooleanValue(item, "InactiveItem$")
        };
    }

    private static bool MatchesItem(ItemDto item, string? query, string? productLine)
    {
        if (!string.IsNullOrWhiteSpace(productLine) &&
            !string.Equals(item.ProductLine, productLine, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return item.ItemCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (item.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
               (item.UpcCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
               (item.EanCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static ItemWarehouseAvailabilityDto ExtractWarehouseAvailability(dynamic itemWhseSvc, string warehouseCode)
    {
        var quantityOnHand = FirstDecimal(
            GetDecimalValue(itemWhseSvc, "QuantityOnHand"),
            GetDecimalValue(itemWhseSvc, "QtyOnHand"));
        var committed = FirstDecimal(
            GetDecimalValue(itemWhseSvc, "QuantityCommitted"),
            GetDecimalValue(itemWhseSvc, "QtyCommitted"));
        var onSalesOrder = FirstDecimal(
            GetDecimalValue(itemWhseSvc, "QuantityOnSalesOrder"),
            GetDecimalValue(itemWhseSvc, "QtyOnSalesOrder"));
        var onPo = FirstDecimal(
            GetDecimalValue(itemWhseSvc, "QuantityOnPurchaseOrder"),
            GetDecimalValue(itemWhseSvc, "QtyOnPurchaseOrder"));
        var available = FirstDecimal(
            GetDecimalValue(itemWhseSvc, "QuantityAvailable"),
            quantityOnHand.HasValue && committed.HasValue ? quantityOnHand.Value - committed.Value : null);

        return new ItemWarehouseAvailabilityDto
        {
            WarehouseCode = warehouseCode,
            QuantityOnHand = quantityOnHand,
            QuantityAvailable = available,
            QuantityCommitted = committed,
            QuantityOnPurchaseOrder = onPo,
            QuantityOnSalesOrder = onSalesOrder
        };
    }

    private decimal? TryCalcQuantityAvailable(dynamic itemWhseSvc, string itemCode, string warehouseCode)
    {
        try
        {
            var result = itemWhseSvc.CalcQuantityAvailable(itemCode, warehouseCode);
            if (decimal.TryParse(result?.ToString(), out decimal parsed))
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CalcQuantityAvailable failed for {ItemCode}/{Warehouse}", itemCode, warehouseCode);
        }

        return null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static decimal? FirstDecimal(params decimal?[] values)
    {
        return values.FirstOrDefault(v => v.HasValue);
    }
}
