using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface ICustomerAccountService
{
    Task<CustomerAccountSummaryResponse?> GetAccountSummaryAsync(
        string customerNumber,
        int openInvoiceLimit,
        CancellationToken cancellationToken = default);
}
