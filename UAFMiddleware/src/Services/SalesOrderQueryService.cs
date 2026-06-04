using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class SalesOrderQueryService : SageReadServiceBase, ISalesOrderQueryService
{
    private const int MaxScan = 1500;

    public SalesOrderQueryService(IProvideXSessionManager sessionManager, ILogger<SalesOrderQueryService> logger)
        : base(sessionManager, logger)
    {
    }

    public Task<SalesOrderSearchResponse> SearchSalesOrdersAsync(
        string? customerNumber,
        string? poNumber,
        string? dateFrom,
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var (divisionNo, customerNo) = ParseCustomerNumber(customerNumber);

        return WithSageObjectAsync("SO_SalesOrder_svc", salesOrderSvc =>
        {
            var response = new SalesOrderSearchResponse();
            if (!MoveFirst(salesOrderSvc))
            {
                return response;
            }

            var hasMore = true;
            while (hasMore && response.SalesOrders.Count < safeLimit && response.ScannedCount < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.ScannedCount++;

                var summary = ExtractSummary(salesOrderSvc);
                if (Matches(summary, divisionNo, customerNo, poNumber, dateFrom, status))
                {
                    response.SalesOrders.Add(summary);
                }

                hasMore = MoveNext(salesOrderSvc);
            }

            response.TotalCount = response.SalesOrders.Count;
            LogScanLimit("SO_SalesOrder_svc", response.ScannedCount, MaxScan);
            return response;
        }, cancellationToken);
    }

    private static SalesOrderSummaryDto ExtractSummary(dynamic salesOrderSvc)
    {
        var divisionNo = GetStringValue(salesOrderSvc, "ARDivisionNo$").Trim();
        var customerNo = GetStringValue(salesOrderSvc, "CustomerNo$").Trim();

        return new SalesOrderSummaryDto
        {
            SalesOrderNumber = GetStringValue(salesOrderSvc, "SalesOrderNo$").Trim(),
            CustomerNumber = !string.IsNullOrWhiteSpace(divisionNo) && !string.IsNullOrWhiteSpace(customerNo)
                ? $"{divisionNo}-{customerNo}"
                : customerNo,
            CustomerName = GetStringValue(salesOrderSvc, "CustomerName$"),
            CustomerPONumber = GetStringValue(salesOrderSvc, "CustomerPONo$"),
            OrderDate = GetStringValue(salesOrderSvc, "OrderDate$"),
            ShipExpireDate = GetStringValue(salesOrderSvc, "ShipExpireDate$"),
            OrderStatus = GetStringValue(salesOrderSvc, "OrderStatus$"),
            OrderTotal = FirstDecimal(
                GetDecimalValue(salesOrderSvc, "OrderTotal"),
                GetDecimalValue(salesOrderSvc, "TaxableSalesAmt"))
        };
    }

    private static bool Matches(
        SalesOrderSummaryDto summary,
        string? divisionNo,
        string? customerNo,
        string? poNumber,
        string? dateFrom,
        string? status)
    {
        if (!string.IsNullOrWhiteSpace(customerNo))
        {
            var expectedCustomer = !string.IsNullOrWhiteSpace(divisionNo) ? $"{divisionNo}-{customerNo}" : customerNo;
            if (!string.Equals(summary.CustomerNumber, expectedCustomer, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(summary.CustomerNumber, customerNo, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(poNumber) &&
            (summary.CustomerPONumber?.Contains(poNumber, StringComparison.OrdinalIgnoreCase) != true))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) &&
            string.Compare(summary.OrderDate, dateFrom, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(summary.OrderStatus, status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static (string? DivisionNo, string? CustomerNo) ParseCustomerNumber(string? customerNumber)
    {
        if (string.IsNullOrWhiteSpace(customerNumber))
        {
            return (null, null);
        }

        var value = customerNumber.Trim();
        if (value.Length > 3 && value[2] == '-')
        {
            return (value[..2], value[3..]);
        }

        return (null, value);
    }

    private static decimal? FirstDecimal(params decimal?[] values)
    {
        return values.FirstOrDefault(v => v.HasValue);
    }
}
