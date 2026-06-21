using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class VendorService : SageReadServiceBase, IVendorService
{
    private const int MaxScan = 25000;

    public VendorService(IProvideXSessionManager sessionManager, ILogger<VendorService> logger)
        : base(sessionManager, logger)
    {
    }

    public Task<VendorDto?> GetVendorAsync(string vendorNumber, CancellationToken cancellationToken = default)
    {
        var (divisionNo, vendorNo) = ParseVendorNumber(vendorNumber);
        return WithSageObjectAsync<VendorDto?>("AP_Vendor_bus", vendor =>
        {
            if (!TryFind(vendor, ("APDivisionNo$", divisionNo), ("VendorNo$", vendorNo)))
            {
                return null;
            }

            return ExtractVendor(vendor);
        }, cancellationToken);
    }

    public Task<VendorSearchResponse> SearchVendorsAsync(
        string? query,
        string? city,
        string? state,
        int limit,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var safeOffset = Math.Max(offset, 0);
        return WithSageObjectAsync("AP_Vendor_svc", vendorSvc =>
        {
            var response = new VendorSearchResponse
            {
                Limit = safeLimit,
                Offset = safeOffset
            };
            if (!MoveFirst(vendorSvc))
            {
                return response;
            }

            var hasMore = true;
            var totalMatches = 0;
            while (hasMore && response.ScannedCount < MaxScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                response.ScannedCount++;

                var vendor = ExtractVendor(vendorSvc);
                if (MatchesVendor(vendor, query, city, state))
                {
                    totalMatches++;
                    if (totalMatches > safeOffset && response.Vendors.Count < safeLimit)
                    {
                        response.Vendors.Add(vendor);
                    }
                }

                hasMore = MoveNext(vendorSvc);
            }

            response.TotalCount = totalMatches;
            response.ReturnedCount = response.Vendors.Count;
            response.HasMore = hasMore || totalMatches > safeOffset + response.Vendors.Count;
            response.ScanLimitReached = hasMore && response.ScannedCount >= MaxScan;
            LogScanLimit("AP_Vendor_svc", response.ScannedCount, MaxScan);
            return response;
        }, cancellationToken);
    }

    private static VendorDto ExtractVendor(dynamic vendor)
    {
        var divisionNo = GetStringValue(vendor, "APDivisionNo$").Trim();
        var vendorNo = GetStringValue(vendor, "VendorNo$").Trim();

        return new VendorDto
        {
            VendorNumber = !string.IsNullOrWhiteSpace(divisionNo) ? $"{divisionNo}-{vendorNo}" : vendorNo,
            APDivisionNo = divisionNo,
            VendorNo = vendorNo,
            VendorName = GetStringValue(vendor, "VendorName$"),
            Status = FirstNonEmpty(GetStringValue(vendor, "VendorStatus$"), GetStringValue(vendor, "Status$")),
            Address1 = GetStringValue(vendor, "AddressLine1$"),
            Address2 = GetStringValue(vendor, "AddressLine2$"),
            City = GetStringValue(vendor, "City$"),
            State = GetStringValue(vendor, "State$"),
            ZipCode = GetStringValue(vendor, "ZipCode$"),
            Phone = GetStringValue(vendor, "TelephoneNo$"),
            TermsCode = GetStringValue(vendor, "TermsCode$")
        };
    }

    private static bool MatchesVendor(VendorDto vendor, string? query, string? city, string? state)
    {
        if (!string.IsNullOrWhiteSpace(query) &&
            !vendor.VendorNumber.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            (vendor.VendorName?.Contains(query, StringComparison.OrdinalIgnoreCase) != true))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(city) &&
            (vendor.City?.Contains(city, StringComparison.OrdinalIgnoreCase) != true))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state) &&
            !string.Equals(vendor.State, state, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static (string DivisionNo, string VendorNo) ParseVendorNumber(string vendorNumber)
    {
        var normalized = vendorNumber.Trim();
        if (normalized.Length > 3 && normalized[2] == '-')
        {
            return (normalized[..2], normalized[3..]);
        }

        return ("00", normalized);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}
