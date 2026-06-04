using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[RequireApiScope(ApiScopes.Read)]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<PurchaseOrderSearchResponse>> SearchPurchaseOrders(
        [FromQuery] string? vendorNumber,
        [FromQuery] string? orderType,
        [FromQuery] string? status,
        [FromQuery] string? dateFrom,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _purchaseOrderService.SearchPurchaseOrdersAsync(
            vendorNumber,
            orderType,
            status,
            dateFrom,
            Math.Min(limit, 100),
            cancellationToken));
    }

    [HttpGet("quotes/search")]
    public async Task<ActionResult<PurchaseOrderSearchResponse>> SearchPurchaseOrderQuotes(
        [FromQuery] string? vendorNumber,
        [FromQuery] string? status,
        [FromQuery] string? dateFrom,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _purchaseOrderService.SearchPurchaseOrdersAsync(
            vendorNumber,
            "Q",
            status,
            dateFrom,
            Math.Min(limit, 100),
            cancellationToken));
    }

    [HttpGet("{purchaseOrderNumber}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetPurchaseOrder(
        string purchaseOrderNumber,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.GetPurchaseOrderAsync(purchaseOrderNumber, cancellationToken);
        if (purchaseOrder == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Purchase order '{purchaseOrderNumber}' was not found",
                ErrorCode = "PURCHASE_ORDER_NOT_FOUND"
            });
        }

        return Ok(purchaseOrder);
    }
}
