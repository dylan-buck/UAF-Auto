using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class CustomerAccountService : SageReadServiceBase, ICustomerAccountService
{
    private const int MaxInvoiceScan = 1500;
    private readonly ICustomerService _customerService;

    public CustomerAccountService(
        IProvideXSessionManager sessionManager,
        ICustomerService customerService,
        ILogger<CustomerAccountService> logger)
        : base(sessionManager, logger)
    {
        _customerService = customerService;
    }

    public async Task<CustomerAccountSummaryResponse?> GetAccountSummaryAsync(
        string customerNumber,
        int openInvoiceLimit,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerService.GetCustomerAsync(customerNumber, cancellationToken);
        if (customer == null)
        {
            return null;
        }

        var response = new CustomerAccountSummaryResponse
        {
            CustomerNumber = customer.CustomerNumber,
            CustomerName = customer.CustomerName,
            Status = customer.Status
        };

        var (divisionNo, customerNo) = ParseCustomerNumber(customer.CustomerNumber);

        await WithSageObjectAsync("AR_Customer_bus", customerBus =>
        {
            if (TryFind(customerBus, ("ARDivisionNo$", divisionNo), ("CustomerNo$", customerNo)))
            {
                response.CreditHold = FirstNonEmpty(
                    GetStringValue(customerBus, "CreditHold$"),
                    GetStringValue(customerBus, "CreditHold"));
                response.CreditLimit = GetDecimalValue(customerBus, "CreditLimit");
                response.CurrentBalance = FirstDecimal(
                    GetDecimalValue(customerBus, "CurrentBalance"),
                    GetDecimalValue(customerBus, "Balance"));
                response.LastInvoiceDate = FirstNonEmpty(
                    GetStringValue(customerBus, "LastInvoiceDate$"),
                    GetStringValue(customerBus, "DateLastInvoice$"));
                response.LastInvoiceAmount = FirstDecimal(
                    GetDecimalValue(customerBus, "LastInvoiceAmount"),
                    GetDecimalValue(customerBus, "AmountLastInvoice"));
            }

            return true;
        }, cancellationToken);

        await WithSageObjectAsync("AR_OpenInvoice_Svc", openInvoiceSvc =>
        {
            if (!MoveFirst(openInvoiceSvc))
            {
                return true;
            }

            var scanned = 0;
            var hasMore = true;
            var safeLimit = Math.Clamp(openInvoiceLimit, 0, 100);
            decimal balance = 0;

            while (hasMore && scanned < MaxInvoiceScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var recordDiv = GetStringValue(openInvoiceSvc, "ARDivisionNo$").Trim();
                var recordCustomer = GetStringValue(openInvoiceSvc, "CustomerNo$").Trim();
                if (recordDiv == divisionNo && recordCustomer == customerNo)
                {
                    var invoiceBalance = FirstDecimal(
                        GetDecimalValue(openInvoiceSvc, "Balance"),
                        GetDecimalValue(openInvoiceSvc, "InvoiceBalance"),
                        GetDecimalValue(openInvoiceSvc, "AmountDue"));

                    response.OpenInvoiceCount++;
                    if (invoiceBalance.HasValue)
                    {
                        balance += invoiceBalance.Value;
                    }

                    if (response.OpenInvoices.Count < safeLimit)
                    {
                        response.OpenInvoices.Add(new OpenInvoiceSummaryDto
                        {
                            InvoiceNo = FirstNonEmpty(
                                GetStringValue(openInvoiceSvc, "InvoiceNo$"),
                                GetStringValue(openInvoiceSvc, "InvoiceNumber$")),
                            InvoiceDate = GetStringValue(openInvoiceSvc, "InvoiceDate$"),
                            DueDate = GetStringValue(openInvoiceSvc, "DueDate$"),
                            Balance = invoiceBalance,
                            InvoiceAmount = FirstDecimal(
                                GetDecimalValue(openInvoiceSvc, "InvoiceAmt"),
                                GetDecimalValue(openInvoiceSvc, "InvoiceAmount"))
                        });
                    }
                }

                hasMore = MoveNext(openInvoiceSvc);
            }

            response.OpenInvoiceBalance = balance;
            LogScanLimit("AR_OpenInvoice_Svc", scanned, MaxInvoiceScan);
            return true;
        }, cancellationToken);

        return response;
    }

    private static (string DivisionNo, string CustomerNo) ParseCustomerNumber(string customerNumber)
    {
        if (customerNumber.Length > 3 && customerNumber[2] == '-')
        {
            return (customerNumber[..2], customerNumber[3..]);
        }

        return ("00", customerNumber);
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
