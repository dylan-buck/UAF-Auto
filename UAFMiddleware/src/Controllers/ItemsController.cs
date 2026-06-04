using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/items")]
[RequireApiScope(ApiScopes.Read)]
public class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(IItemService itemService, ILogger<ItemsController> logger)
    {
        _itemService = itemService;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<ActionResult<ItemSearchResponse>> SearchItems(
        [FromQuery] string? q,
        [FromQuery] string? productLine,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _itemService.SearchItemsAsync(q, productLine, Math.Min(limit, 100), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{itemCode}")]
    public async Task<ActionResult<ItemDto>> GetItem(string itemCode, CancellationToken cancellationToken)
    {
        var item = await _itemService.GetItemAsync(itemCode, cancellationToken);
        if (item == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Item '{itemCode}' was not found",
                ErrorCode = "ITEM_NOT_FOUND"
            });
        }

        return Ok(item);
    }

    [HttpGet("{itemCode}/availability")]
    public async Task<ActionResult<ItemAvailabilityDto>> GetItemAvailability(
        string itemCode,
        [FromQuery] string? warehouseCode,
        CancellationToken cancellationToken)
    {
        var result = await _itemService.GetAvailabilityAsync(new ItemAvailabilityRequest
        {
            Items =
            [
                new ItemAvailabilityRequestLine
                {
                    ItemCode = itemCode,
                    WarehouseCode = warehouseCode
                }
            ]
        }, cancellationToken);

        return Ok(result.Items.FirstOrDefault() ?? new ItemAvailabilityDto { ItemCode = itemCode });
    }

    [HttpPost("availability")]
    public async Task<ActionResult<ItemAvailabilityResponse>> GetBulkAvailability(
        [FromBody] ItemAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "At least one item is required",
                ErrorCode = "MISSING_ITEMS"
            });
        }

        var result = await _itemService.GetAvailabilityAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{itemCode}/aliases")]
    public async Task<ActionResult<ItemRelatedItemsResponse>> GetAliases(string itemCode, CancellationToken cancellationToken)
    {
        return Ok(await _itemService.GetAliasesAsync(itemCode, cancellationToken));
    }

    [HttpGet("{itemCode}/alternates")]
    public async Task<ActionResult<ItemRelatedItemsResponse>> GetAlternates(string itemCode, CancellationToken cancellationToken)
    {
        return Ok(await _itemService.GetAlternatesAsync(itemCode, cancellationToken));
    }
}
