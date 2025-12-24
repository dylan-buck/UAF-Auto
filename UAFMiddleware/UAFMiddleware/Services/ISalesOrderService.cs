using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface ISalesOrderService
{
    Task<SalesOrderResponse> CreateSalesOrderAsync(SalesOrderRequest request, CancellationToken cancellationToken = default);
}



