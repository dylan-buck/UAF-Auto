using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService inventoryService,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Validate that all provided item codes exist in Sage 100.
    /// Use this before creating a sales order to ensure all SKUs are valid.
    /// </summary>
    /// <param name="request">Request containing list of item codes to validate</param>
    [HttpPost("validate")]
    public async Task<ActionResult<ItemValidationResult>> ValidateItemCodes(
        [FromBody] ItemValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemCodes == null || request.ItemCodes.Count == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "At least one item code is required",
                ErrorCode = "MISSING_ITEM_CODES"
            });
        }

        _logger.LogInformation("Validating {Count} item codes", request.ItemCodes.Count);

        try
        {
            var result = await _inventoryService.ValidateItemCodesAsync(
                request.ItemCodes, cancellationToken);

            _logger.LogInformation(
                "Item validation complete: {Valid}/{Total} valid, {Invalid} invalid",
                result.ValidItemCodes.Count,
                result.TotalChecked,
                result.InvalidItemCodes.Count);

            // Return 200 even if some items are invalid - the result contains the details
            return Ok(result);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout validating item codes");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating item codes");
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Check if a single item code exists in Sage 100
    /// </summary>
    /// <param name="itemCode">The item code to check</param>
    [HttpGet("check/{itemCode}")]
    public async Task<ActionResult<object>> CheckItemExists(
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Item code is required",
                ErrorCode = "MISSING_ITEM_CODE"
            });
        }

        _logger.LogInformation("Checking if item '{ItemCode}' exists", itemCode);

        try
        {
            bool exists = await _inventoryService.ItemExistsAsync(itemCode, cancellationToken);

            return Ok(new
            {
                itemCode = itemCode,
                exists = exists,
                message = exists ? "Item exists in Sage 100" : "Item not found in Sage 100"
            });
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout checking item");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking item {ItemCode}", itemCode);
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Test endpoint - verifies the Inventory API is working
    /// </summary>
    [HttpGet("test")]
    public ActionResult<object> Test()
    {
        return Ok(new
        {
            message = "Inventory API is working",
            timestamp = DateTime.UtcNow,
            endpoints = new
            {
                validate = "POST /api/v1/inventory/validate",
                check = "GET /api/v1/inventory/check/{itemCode}"
            }
        });
    }
}

/// <summary>
/// Request to validate multiple item codes
/// </summary>
public class ItemValidationRequest
{
    /// <summary>
    /// List of item codes (SKUs) to validate
    /// </summary>
    public List<string> ItemCodes { get; set; } = new();
}
