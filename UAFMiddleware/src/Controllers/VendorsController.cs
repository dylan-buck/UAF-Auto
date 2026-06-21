using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/vendors")]
[RequireApiScope(ApiScopes.Read)]
public class VendorsController : ControllerBase
{
    private readonly IVendorService _vendorService;

    public VendorsController(IVendorService vendorService)
    {
        _vendorService = vendorService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<VendorSearchResponse>> SearchVendors(
        [FromQuery] string? q,
        [FromQuery] string? city,
        [FromQuery] string? state,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _vendorService.SearchVendorsAsync(
            q,
            city,
            state,
            Math.Clamp(limit, 1, 100),
            Math.Max(offset, 0),
            cancellationToken));
    }

    [HttpGet("{vendorNumber}")]
    public async Task<ActionResult<VendorDto>> GetVendor(string vendorNumber, CancellationToken cancellationToken)
    {
        var vendor = await _vendorService.GetVendorAsync(vendorNumber, cancellationToken);
        if (vendor == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Vendor '{vendorNumber}' was not found",
                ErrorCode = "VENDOR_NOT_FOUND"
            });
        }

        return Ok(vendor);
    }
}
