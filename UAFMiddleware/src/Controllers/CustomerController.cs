using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/customers")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICustomerAccountService _customerAccountService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(
        ICustomerService customerService,
        ICustomerAccountService customerAccountService,
        ILogger<CustomerController> logger)
    {
        _customerService = customerService;
        _customerAccountService = customerAccountService;
        _logger = logger;
    }

    /// <summary>
    /// Search for customers by name, address, city, state, or phone
    /// </summary>
    /// <param name="name">Customer name (partial match)</param>
    /// <param name="address">Address line (partial match)</param>
    /// <param name="city">City (partial match)</param>
    /// <param name="state">State (exact match)</param>
    /// <param name="phone">Phone number</param>
    /// <param name="limit">Maximum results (default 20)</param>
    [HttpGet("search")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<CustomerSearchResponse>> SearchCustomers(
        [FromQuery] string? name,
        [FromQuery] string? address,
        [FromQuery] string? city,
        [FromQuery] string? state,
        [FromQuery] string? phone,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Customer search request: Name={Name}, City={City}, State={State}, Phone={Phone}",
            name, city, state, phone);

        // Validate at least one search parameter
        if (string.IsNullOrEmpty(name) && 
            string.IsNullOrEmpty(address) &&
            string.IsNullOrEmpty(city) && 
            string.IsNullOrEmpty(state) &&
            string.IsNullOrEmpty(phone))
        {
            return BadRequest(CreateErrorResponse(
                "At least one search parameter is required",
                "MISSING_SEARCH_PARAMS"));
        }

        try
        {
            var request = new CustomerSearchRequest
            {
                Name = name,
                Address = address,
                City = city,
                State = state,
                Phone = phone,
                Limit = Math.Min(limit, 100) // Cap at 100
            };

            var result = await _customerService.SearchCustomersAsync(request, cancellationToken);
            
            _logger.LogInformation("Customer search returned {Count} results", result.TotalCount);
            
            return Ok(result);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout searching customers");
            return BusyResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers");
            return InternalErrorResponse("Customer search failed");
        }
    }

    /// <summary>
    /// Get a specific customer by customer number
    /// </summary>
    /// <param name="customerNumber">Customer number in format "01-D3375"</param>
    [HttpGet("{customerNumber}")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<CustomerDto>> GetCustomer(
        string customerNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Get customer request: {CustomerNumber}", customerNumber);

        try
        {
            var customer = await _customerService.GetCustomerAsync(customerNumber, cancellationToken);
            
            if (customer == null)
            {
                return NotFound(CreateErrorResponse(
                    $"Customer '{customerNumber}' not found",
                    "CUSTOMER_NOT_FOUND"));
            }

            return Ok(customer);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout getting customer");
            return BusyResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {CustomerNumber}", customerNumber);
            return InternalErrorResponse($"Failed to get customer {customerNumber}");
        }
    }

    /// <summary>
    /// Get finance-sensitive customer account summary facts.
    /// Requires the finance API scope.
    /// </summary>
    [HttpGet("{customerNumber}/account-summary")]
    [RequireApiScope(ApiScopes.Read)]
    [RequireApiScope(ApiScopes.Finance)]
    public async Task<ActionResult<CustomerAccountSummaryResponse>> GetAccountSummary(
        string customerNumber,
        [FromQuery] int openInvoiceLimit = 10,
        CancellationToken cancellationToken = default)
    {
        var summary = await _customerAccountService.GetAccountSummaryAsync(
            customerNumber,
            Math.Min(openInvoiceLimit, 100),
            cancellationToken);

        if (summary == null)
        {
            return NotFound(CreateErrorResponse(
                $"Customer '{customerNumber}' not found",
                "CUSTOMER_NOT_FOUND"));
        }

        return Ok(summary);
    }

    /// <summary>
    /// Validate if a ship-to address matches the customer's default ship-to
    /// </summary>
    [HttpPost("{customerNumber}/validate-shipto")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<ValidateShipToResponse>> ValidateShipTo(
        string customerNumber,
        [FromBody] ValidateShipToRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Validate ship-to for customer {CustomerNumber}: City={City}, State={State}",
            customerNumber, request.City, request.State);

        try
        {
            var result = await _customerService.ValidateShipToAsync(
                customerNumber, request, cancellationToken);
            
            _logger.LogInformation(
                "Ship-to validation result: Matched={Matched}, IsDefault={IsDefault}, Confidence={Confidence}",
                result.Matched, result.IsDefaultShipTo, result.MatchConfidence);

            return Ok(result);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout validating ship-to");
            return BusyResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ship-to for {CustomerNumber}", customerNumber);
            return InternalErrorResponse($"Ship-to validation failed for {customerNumber}");
        }
    }

    /// <summary>
    /// Resolve the best customer/ship-to match from PO data using intelligent scoring.
    /// Returns matching facts and confidence only; business pass/fail policy belongs in automation.
    /// </summary>
    [HttpPost("resolve")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<CustomerResolutionResponse>> ResolveCustomer(
        [FromBody] CustomerResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Customer resolution request: Name={Name}, ShipToCity={City}, ShipToState={State}",
            request.CustomerName,
            request.ShipToAddress?.City,
            request.ShipToAddress?.State);

        if (string.IsNullOrEmpty(request.CustomerName))
        {
            return BadRequest(CreateErrorResponse(
                "Customer name is required",
                "MISSING_CUSTOMER_NAME"));
        }

        try
        {
            var result = await _customerService.ResolveCustomerAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Customer resolution: {Recommendation}, Confidence={Confidence:P0}, Match={CustomerNumber}",
                result.Recommendation, 
                result.Confidence,
                result.BestMatch?.CustomerNumber ?? "none");

            return Ok(result);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout resolving customer");
            return BusyResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving customer");
            return InternalErrorResponse("Customer resolution failed");
        }
    }

    /// <summary>
    /// Test endpoint - verifies the Customer API is working
    /// </summary>
    [HttpGet("test")]
    [RequireApiScope(ApiScopes.Read)]
    public ActionResult<object> Test()
    {
        return Ok(new
        {
            message = "Customer API is working",
            timestamp = DateTime.UtcNow,
            endpoints = new
            {
                search = "GET /api/v1/customers/search?name=&city=&state=&phone=",
                getCustomer = "GET /api/v1/customers/{customerNumber}",
                validateShipTo = "POST /api/v1/customers/{customerNumber}/validate-shipto",
                resolve = "POST /api/v1/customers/resolve"
            }
        });
    }

    private ObjectResult BusyResponse()
    {
        return StatusCode(503, CreateErrorResponse(
            "Service is busy, please try again",
            "SERVICE_BUSY"));
    }

    private ObjectResult InternalErrorResponse(string message)
    {
        return StatusCode(500, CreateErrorResponse(message, "INTERNAL_ERROR"));
    }

    private static ErrorResponse CreateErrorResponse(string error, string errorCode)
    {
        return new ErrorResponse
        {
            Error = error,
            ErrorCode = errorCode
        };
    }
}
