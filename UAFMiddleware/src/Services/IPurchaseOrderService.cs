using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto?> GetPurchaseOrderAsync(string purchaseOrderNumber, CancellationToken cancellationToken = default);
    Task<PurchaseOrderSearchResponse> SearchPurchaseOrdersAsync(
        string? vendorNumber,
        string? orderType,
        string? status,
        string? dateFrom,
        int limit,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
