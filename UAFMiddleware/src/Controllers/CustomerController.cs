using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/customers")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(
        ICustomerService customerService,
        ILogger<CustomerController> logger)
    {
        _customerService = customerService;
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
            return BadRequest(new ErrorResponse
            {
                Error = "At least one search parameter is required",
                ErrorCode = "MISSING_SEARCH_PARAMS"
            });
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
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers");
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Get a specific customer by customer number
    /// </summary>
    /// <param name="customerNumber">Customer number in format "01-D3375"</param>
    [HttpGet("{customerNumber}")]
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
                return NotFound(new ErrorResponse
                {
                    Error = $"Customer '{customerNumber}' not found",
                    ErrorCode = "CUSTOMER_NOT_FOUND"
                });
            }

            return Ok(customer);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout getting customer");
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {CustomerNumber}", customerNumber);
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Validate if a ship-to address matches the customer's default ship-to
    /// </summary>
    [HttpPost("{customerNumber}/validate-shipto")]
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
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ship-to for {CustomerNumber}", customerNumber);
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Resolve the correct customer from PO data using intelligent matching.
    /// Returns the best match with confidence score and recommendation.
    /// </summary>
    [HttpPost("resolve")]
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
            return BadRequest(new ErrorResponse
            {
                Error = "Customer name is required",
                ErrorCode = "MISSING_CUSTOMER_NAME"
            });
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
            return StatusCode(503, new ErrorResponse
            {
                Error = "Service is busy, please try again",
                ErrorCode = "SERVICE_BUSY"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving customer");
            return StatusCode(500, new ErrorResponse
            {
                Error = "An unexpected error occurred",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Test endpoint - verifies the Customer API is working
    /// </summary>
    [HttpGet("test")]
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
}

