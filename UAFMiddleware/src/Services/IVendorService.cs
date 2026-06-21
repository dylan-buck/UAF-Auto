using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface IVendorService
{
    Task<VendorDto?> GetVendorAsync(string vendorNumber, CancellationToken cancellationToken = default);
    Task<VendorSearchResponse> SearchVendorsAsync(string? query, string? city, string? state, int limit, int offset = 0, CancellationToken cancellationToken = default);
}
