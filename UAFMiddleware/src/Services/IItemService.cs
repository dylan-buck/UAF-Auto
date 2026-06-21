using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface IItemService
{
    Task<ItemDto?> GetItemAsync(string itemCode, CancellationToken cancellationToken = default);
    Task<ItemSearchResponse> SearchItemsAsync(string? query, string? productLine, int limit, int offset = 0, CancellationToken cancellationToken = default);
    Task<ItemAvailabilityResponse> GetAvailabilityAsync(ItemAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<ItemRelatedItemsResponse> GetAliasesAsync(string itemCode, CancellationToken cancellationToken = default);
    Task<ItemRelatedItemsResponse> GetAlternatesAsync(string itemCode, CancellationToken cancellationToken = default);
}
