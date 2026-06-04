using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface IReferenceDataService
{
    Task<ReferenceDataResponse> GetReferenceDataAsync(string type, int limit, CancellationToken cancellationToken = default);
}
