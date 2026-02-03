namespace UAFMiddleware.Services;

public class InventoryService : IInventoryService
{
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(ILogger<InventoryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates item codes. Note: CI_ItemCode_bus is not accessible in this Sage installation,
    /// so we pass all items as valid. Invalid items will be caught when the sales order is created.
    /// </summary>
    public Task<ItemValidationResult> ValidateItemCodesAsync(
        List<string> itemCodes,
        CancellationToken cancellationToken = default)
    {
        var result = new ItemValidationResult
        {
            TotalChecked = itemCodes.Count
        };

        if (itemCodes.Count == 0)
        {
            result.Message = "No item codes provided";
            return Task.FromResult(result);
        }

        _logger.LogInformation("Validating {Count} item codes (pass-through mode - actual validation at order creation)", itemCodes.Count);

        // Pass all non-empty items as valid
        // CI_ItemCode_bus is not accessible in this Sage installation (Error 90)
        // Invalid items will be caught and reported when the sales order is created
        foreach (var itemCode in itemCodes)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                result.InvalidItemCodes.Add(itemCode ?? "(empty)");
            }
            else
            {
                result.ValidItemCodes.Add(itemCode.Trim());
            }
        }

        if (result.AllValid)
        {
            result.Message = $"All {result.TotalChecked} item codes accepted (will be validated at order creation)";
        }
        else
        {
            result.Message = $"{result.InvalidItemCodes.Count} empty item codes filtered out";
        }

        _logger.LogInformation(result.Message);
        return Task.FromResult(result);
    }

    public Task<bool> ItemExistsAsync(
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        // Pass-through mode - return true for non-empty items
        // Actual validation happens at sales order creation
        return Task.FromResult(!string.IsNullOrWhiteSpace(itemCode));
    }
}
