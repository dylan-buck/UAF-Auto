using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface ISalesOrderQueryService
{
    Task<SalesOrderSearchResponse> SearchSalesOrdersAsync(
        string? customerNumber,
        string? poNumber,
        string? dateFrom,
        string? status,
        int limit,
        CancellationToken cancellationToken = default);
}
