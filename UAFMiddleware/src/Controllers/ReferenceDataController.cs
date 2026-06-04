using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/reference")]
[RequireApiScope(ApiScopes.Read)]
public class ReferenceDataController : ControllerBase
{
    private readonly IReferenceDataService _referenceDataService;

    public ReferenceDataController(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService;
    }

    [HttpGet("{type}")]
    public async Task<ActionResult<ReferenceDataResponse>> GetReferenceData(
        string type,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var response = await _referenceDataService.GetReferenceDataAsync(type, Math.Min(limit, 100), cancellationToken);
        if (response.Items.Count == 0)
        {
            return NotFound(new ErrorResponse
            {
                Error = $"Reference data type '{type}' was not found or returned no records",
                ErrorCode = "REFERENCE_DATA_NOT_FOUND"
            });
        }

        return Ok(response);
    }
}
