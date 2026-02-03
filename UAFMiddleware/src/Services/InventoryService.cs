using System.Runtime.InteropServices;

namespace UAFMiddleware.Services;

public class InventoryService : IInventoryService
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IProvideXSessionManager sessionManager,
        ILogger<InventoryService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<ItemValidationResult> ValidateItemCodesAsync(
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
            return result;
        }

        _logger.LogInformation("Validating {Count} item codes", itemCodes.Count);

        SessionWrapper? session = null;
        dynamic? itemSvc = null;

        try
        {
            session = await _sessionManager.GetSessionAsync(cancellationToken);

            // Create CI_Item_svc object for looking up items
            itemSvc = session.ProvideXScript.NewObject("CI_Item_svc", session.Session);

            if (itemSvc == null)
            {
                throw new InvalidOperationException("Failed to create CI_Item_svc object");
            }

            foreach (var itemCode in itemCodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    result.InvalidItemCodes.Add(itemCode ?? "(empty)");
                    continue;
                }

                bool exists = await CheckItemExistsInternal(itemSvc, itemCode.Trim());

                if (exists)
                {
                    result.ValidItemCodes.Add(itemCode.Trim());
                    _logger.LogDebug("Item '{ItemCode}' is valid", itemCode);
                }
                else
                {
                    result.InvalidItemCodes.Add(itemCode.Trim());
                    _logger.LogWarning("Item '{ItemCode}' not found in Sage", itemCode);
                }
            }

            // Build result message
            if (result.AllValid)
            {
                result.Message = $"All {result.TotalChecked} item codes are valid";
                _logger.LogInformation(result.Message);
            }
            else
            {
                result.Message = $"{result.InvalidItemCodes.Count} of {result.TotalChecked} item codes are invalid: {string.Join(", ", result.InvalidItemCodes)}";
                _logger.LogWarning(result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating item codes");
            throw;
        }
        finally
        {
            if (itemSvc != null && Marshal.IsComObject(itemSvc))
            {
                Marshal.ReleaseComObject(itemSvc);
            }

            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    public async Task<bool> ItemExistsAsync(
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return false;
        }

        SessionWrapper? session = null;
        dynamic? itemSvc = null;

        try
        {
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            itemSvc = session.ProvideXScript.NewObject("CI_Item_svc", session.Session);

            if (itemSvc == null)
            {
                throw new InvalidOperationException("Failed to create CI_Item_svc object");
            }

            return await CheckItemExistsInternal(itemSvc, itemCode.Trim());
        }
        finally
        {
            if (itemSvc != null && Marshal.IsComObject(itemSvc))
            {
                Marshal.ReleaseComObject(itemSvc);
            }

            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    private Task<bool> CheckItemExistsInternal(dynamic itemSvc, string itemCode)
    {
        try
        {
            // Use nSetKeyValue + nSetKey pattern (matches working VBS pattern)
            itemSvc.nSetKeyValue("ItemCode$", itemCode);
            object setKeyResult = itemSvc.nSetKey();
            int found = setKeyResult != null ? Convert.ToInt32(setKeyResult) : 0;

            // nSetKey returns 1 if exact key exists, 0 if not found
            return Task.FromResult(found == 1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if item '{ItemCode}' exists", itemCode);
            return Task.FromResult(false);
        }
    }

    private string GetStringValue(dynamic obj, string fieldName)
    {
        try
        {
            string value = "";
            obj.nGetValue(fieldName, ref value);
            return value ?? "";
        }
        catch
        {
            return "";
        }
    }
}
