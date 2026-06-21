using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class PurchaseOrderService : SageReadServiceBase, IPurchaseOrderService
{
    private const int MaxScan = 25000;

    public PurchaseOrderService(IProvideXSessionManager sessionManager, ILogger<PurchaseOrderService> logger)
        : base(sessionManager, logger)
    {
    }

    public Task<PurchaseOrderDto?> GetPurchaseOrderAsync(string purchaseOrderNumber, CancellationToken cancellationToken = default)
    {
        var normalized = purchaseOrderNumber.Trim();
        return WithSageObjectAsync<PurchaseOrderDto?>("PO_PurchaseOrder_Bus", purchaseOrder =>
        {
            if (!TryFind(purchaseOrder, ("PurchaseOrderNo$", normalized)))
            {
                return null;
            }

            return ExtractPurchaseOrder(purchaseOrder);
        }, cancellationToken);
    }

    public Task<PurchaseOrderSearchResponse> SearchPurchaseOrdersAsync(
        string? vendorNumber,
        string? orderType,
        string? status,
        string? dateFrom,
        int limit,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var safeOffset = Math.Max(offset, 0);
        var parsedVendor = ParseVendorNumber(vendorNumber);

        return WithSageObjectAsync("PO_PurchaseOrder_svc", purchaseOrderSvc =>
        {
            var response = new PurchaseOrderSearchResponse
            {
                Limit = safeLimit,
                Offset = safeOffset
            };
            if (!MoveFirst(purchaseOrderSvc))
            {
                return response;
            }

            var hasMore = true;
            var totalMatches = 0;
            while (hasMore && response.ScannedCount < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.ScannedCount++;

                var purchaseOrder = ExtractPurchaseOrder(purchaseOrderSvc);
                if (MatchesPurchaseOrder(purchaseOrder, parsedVendor, orderType, status, dateFrom))
                {
                    totalMatches++;
                    if (totalMatches > safeOffset && response.PurchaseOrders.Count < safeLimit)
                    {
                        response.PurchaseOrders.Add(purchaseOrder);
                    }
                }

                hasMore = MoveNext(purchaseOrderSvc);
            }

            response.TotalCount = totalMatches;
            response.ReturnedCount = response.PurchaseOrders.Count;
            response.HasMore = hasMore || totalMatches > safeOffset + response.PurchaseOrders.Count;
            response.ScanLimitReached = hasMore && response.ScannedCount >= MaxScan;
            LogScanLimit("PO_PurchaseOrder_svc", response.ScannedCount, MaxScan);
            return response;
        }, cancellationToken);
    }

    private static PurchaseOrderDto ExtractPurchaseOrder(dynamic purchaseOrder)
    {
        var divisionNo = GetStringValue(purchaseOrder, "APDivisionNo$").Trim();
        var vendorNo = GetStringValue(purchaseOrder, "VendorNo$").Trim();

        return new PurchaseOrderDto
        {
            PurchaseOrderNumber = GetStringValue(purchaseOrder, "PurchaseOrderNo$").Trim(),
            OrderType = FirstNonEmpty(
                GetStringValue(purchaseOrder, "OrderType$"),
                GetStringValue(purchaseOrder, "PurchaseOrderType$")),
            OrderStatus = FirstNonEmpty(
                GetStringValue(purchaseOrder, "OrderStatus$"),
                GetStringValue(purchaseOrder, "Status$")),
            VendorNumber = !string.IsNullOrWhiteSpace(divisionNo) ? $"{divisionNo}-{vendorNo}" : vendorNo,
            VendorName = FirstNonEmpty(
                GetStringValue(purchaseOrder, "PurchaseName$"),
                GetStringValue(purchaseOrder, "VendorName$")),
            OrderDate = GetStringValue(purchaseOrder, "OrderDate$"),
            RequiredExpireDate = FirstNonEmpty(
                GetStringValue(purchaseOrder, "RequiredExpireDate$"),
                GetStringValue(purchaseOrder, "RequiredDate$"),
                GetStringValue(purchaseOrder, "ExpireDate$")),
            OrderTotal = FirstDecimal(
                GetDecimalValue(purchaseOrder, "OrderTotal"),
                GetDecimalValue(purchaseOrder, "PurchaseOrderTotal"))
        };
    }

    private static bool MatchesPurchaseOrder(
        PurchaseOrderDto purchaseOrder,
        (string? DivisionNo, string? VendorNo) vendor,
        string? orderType,
        string? status,
        string? dateFrom)
    {
        if (!string.IsNullOrWhiteSpace(vendor.VendorNo))
        {
            var expected = !string.IsNullOrWhiteSpace(vendor.DivisionNo)
                ? $"{vendor.DivisionNo}-{vendor.VendorNo}"
                : vendor.VendorNo;
            if (!string.Equals(purchaseOrder.VendorNumber, expected, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(purchaseOrder.VendorNumber, vendor.VendorNo, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(orderType) &&
            !string.Equals(purchaseOrder.OrderType, orderType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(purchaseOrder.OrderStatus, status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) &&
            string.Compare(purchaseOrder.OrderDate, dateFrom, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return true;
    }

    private static (string? DivisionNo, string? VendorNo) ParseVendorNumber(string? vendorNumber)
    {
        if (string.IsNullOrWhiteSpace(vendorNumber))
        {
            return (null, null);
        }

        var normalized = vendorNumber.Trim();
        if (normalized.Length > 3 && normalized[2] == '-')
        {
            return (normalized[..2], normalized[3..]);
        }

        return (null, normalized);
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
