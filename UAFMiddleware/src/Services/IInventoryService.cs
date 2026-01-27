namespace UAFMiddleware.Services;

/// <summary>
/// Service for inventory-related operations including SKU validation
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Validate that all item codes exist in Sage 100 inventory
    /// </summary>
    /// <param name="itemCodes">List of item codes to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with list of any invalid item codes</returns>
    Task<ItemValidationResult> ValidateItemCodesAsync(
        List<string> itemCodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a single item code exists in Sage 100
    /// </summary>
    /// <param name="itemCode">Item code to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if item exists, false otherwise</returns>
    Task<bool> ItemExistsAsync(
        string itemCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of item code validation
/// </summary>
public class ItemValidationResult
{
    /// <summary>
    /// Whether all items are valid (no invalid codes)
    /// </summary>
    public bool AllValid => InvalidItemCodes.Count == 0;

    /// <summary>
    /// List of item codes that were not found in Sage
    /// </summary>
    public List<string> InvalidItemCodes { get; set; } = new();

    /// <summary>
    /// List of item codes that were validated successfully
    /// </summary>
    public List<string> ValidItemCodes { get; set; } = new();

    /// <summary>
    /// Total number of items checked
    /// </summary>
    public int TotalChecked { get; set; }

    /// <summary>
    /// Human-readable message about the validation result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
