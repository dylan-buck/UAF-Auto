using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class CustomerAccountService : SageReadServiceBase, ICustomerAccountService
{
    private const int MaxInvoiceScan = 25000;
    private const int MaxPositionedInvoiceScan = 5000;
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
            var safeLimit = Math.Clamp(openInvoiceLimit, 0, 100);
            decimal balance = 0;

            bool positioned = TryPositionToOpenInvoices(openInvoiceSvc, divisionNo, customerNo);
            if (!positioned && !MoveFirst(openInvoiceSvc))
            {
                return true;
            }

            var scanned = 0;
            var hasMore = true;
            var scanLimit = positioned ? MaxPositionedInvoiceScan : MaxInvoiceScan;

            while (hasMore && scanned < scanLimit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var recordDiv = GetStringValue(openInvoiceSvc, "ARDivisionNo$").Trim();
                var recordCustomer = GetStringValue(openInvoiceSvc, "CustomerNo$").Trim();

                if (positioned &&
                    (!recordDiv.Equals(divisionNo, StringComparison.OrdinalIgnoreCase) ||
                     !recordCustomer.Equals(customerNo, StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }

                if (recordDiv.Equals(divisionNo, StringComparison.OrdinalIgnoreCase) &&
                    recordCustomer.Equals(customerNo, StringComparison.OrdinalIgnoreCase))
                {
                    AddOpenInvoice(openInvoiceSvc, response, safeLimit, ref balance);
                }

                hasMore = MoveNext(openInvoiceSvc);
            }

            response.OpenInvoiceBalance = balance;
            response.OpenInvoiceReturnedCount = response.OpenInvoices.Count;
            response.OpenInvoiceScannedCount = scanned;
            response.OpenInvoiceHasMore = positioned
                ? hasMore && scanned >= scanLimit
                : hasMore || response.OpenInvoiceCount > response.OpenInvoices.Count;
            response.OpenInvoiceScanLimitReached = hasMore && scanned >= scanLimit;
            LogScanLimit("AR_OpenInvoice_Svc", scanned, scanLimit);
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

    private static bool TryPositionToOpenInvoices(dynamic openInvoiceSvc, string divisionNo, string customerNo)
    {
        try
        {
            openInvoiceSvc.nSetKeyValue("ARDivisionNo$", divisionNo);
            openInvoiceSvc.nSetKeyValue("CustomerNo$", customerNo);
            if (TryFind(openInvoiceSvc, ("ARDivisionNo$", divisionNo), ("CustomerNo$", customerNo)) &&
                IsCurrentOpenInvoiceCustomer(openInvoiceSvc, divisionNo, customerNo))
            {
                return true;
            }
        }
        catch
        {
            // Fall through to broad scan.
        }

        return false;
    }

    private static bool IsCurrentOpenInvoiceCustomer(dynamic openInvoiceSvc, string divisionNo, string customerNo)
    {
        return GetStringValue(openInvoiceSvc, "ARDivisionNo$").Trim().Equals(divisionNo, StringComparison.OrdinalIgnoreCase) &&
               GetStringValue(openInvoiceSvc, "CustomerNo$").Trim().Equals(customerNo, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddOpenInvoice(
        dynamic openInvoiceSvc,
        CustomerAccountSummaryResponse response,
        int safeLimit,
        ref decimal balance)
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

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static decimal? FirstDecimal(params decimal?[] values)
    {
        return values.FirstOrDefault(v => v.HasValue);
    }
}
