using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class ReferenceDataService : SageReadServiceBase, IReferenceDataService
{
    private const int MaxScan = 500;

    private static readonly Dictionary<string, ReferenceDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["warehouses"] = new("IM_Warehouse_svc", "WarehouseCode$", "WarehouseDesc$"),
        ["terms"] = new("AR_TermsCode_Svc", "TermsCode$", "TermsCodeDesc$"),
        ["tax-schedules"] = new("SY_SalesTaxSchedule_Svc", "TaxSchedule$", "TaxScheduleDesc$"),
        ["ar-divisions"] = new("AR_Division_svc", "ARDivisionNo$", "DivisionDesc$"),
        ["ap-divisions"] = new("AP_Division_svc", "APDivisionNo$", "DivisionDesc$"),
        ["cancel-reason-codes"] = new("SO_CancelReasonCode_svc", "CancelReasonCode$", "CancelReasonDesc$")
    };

    public ReferenceDataService(IProvideXSessionManager sessionManager, ILogger<ReferenceDataService> logger)
        : base(sessionManager, logger)
    {
    }

    public Task<ReferenceDataResponse> GetReferenceDataAsync(
        string type,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!Definitions.TryGetValue(type, out var definition))
        {
            return Task.FromResult(new ReferenceDataResponse { Type = type });
        }

        var safeLimit = Math.Clamp(limit, 1, 100);
        return WithSageObjectAsync(definition.ObjectName, svc =>
        {
            var response = new ReferenceDataResponse { Type = type };
            if (!MoveFirst(svc))
            {
                return response;
            }

            var hasMore = true;
            var scanned = 0;
            while (hasMore && response.Items.Count < safeLimit && scanned < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var code = GetStringValue(svc, definition.CodeField).Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    response.Items.Add(new ReferenceDataItemDto
                    {
                        Code = code,
                        Description = GetStringValue(svc, definition.DescriptionField),
                        Fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            [definition.CodeField] = code,
                            [definition.DescriptionField] = GetStringValue(svc, definition.DescriptionField)
                        }
                    });
                }

                hasMore = MoveNext(svc);
            }

            LogScanLimit(definition.ObjectName, scanned, MaxScan);
            return response;
        }, cancellationToken);
    }

    private sealed record ReferenceDefinition(string ObjectName, string CodeField, string DescriptionField);
}
