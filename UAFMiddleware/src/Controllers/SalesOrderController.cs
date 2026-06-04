using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/sales-orders")]
public class SalesOrderController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;
    private readonly ISalesOrderQueryService _salesOrderQueryService;
    private readonly ILogger<SalesOrderController> _logger;

    public SalesOrderController(
        ISalesOrderService salesOrderService,
        ISalesOrderQueryService salesOrderQueryService,
        ILogger<SalesOrderController> logger)
    {
        _salesOrderService = salesOrderService;
        _salesOrderQueryService = salesOrderQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sales order in Sage 100
    /// </summary>
    [HttpPost]
    [RequireApiScope(ApiScopes.Create)]
    public async Task<ActionResult<SalesOrderResponse>> CreateSalesOrder(
        [FromBody] SalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received sales order request: Customer={Customer}, PO={PO}, Lines={Lines}",
            request.CustomerNumber, request.PONumber, request.Lines.Count);

        // Validate request
        if (!ModelState.IsValid)
        {
            return BadRequest(CreateValidationErrorResponse());
        }

        try
        {
            var result = await _salesOrderService.CreateSalesOrderAsync(request, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Sales order created successfully: {OrderNo}",
                    result.SalesOrderNumber);
                
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "Sales order creation failed: {ErrorCode} - {ErrorMessage}",
                    result.ErrorCode, result.ErrorMessage);
                
                // Return 200 for business logic errors so workflows can read the response
                // and route based on success/errorCode fields
                if (result.ErrorCode == "INVALID_ITEM")
                {
                    return Ok(result);
                }

                // Determine appropriate status code for other errors
                return StatusCode(GetStatusCodeForError(result.ErrorCode), result);
            }
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout waiting for available session");
            return StatusCode(503, new SalesOrderResponse
            {
                Success = false,
                ErrorCode = "SERVICE_BUSY",
                ErrorMessage = "Service is busy, please try again"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing sales order request");
            return StatusCode(500, new SalesOrderResponse
            {
                Success = false,
                ErrorCode = "INTERNAL_ERROR",
                ErrorMessage = "Sales order creation failed"
            });
        }
    }

    /// <summary>
    /// Search open sales orders by customer, customer PO, order date, or status.
    /// </summary>
    [HttpGet("search")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<SalesOrderSearchResponse>> SearchSalesOrders(
        [FromQuery] string? customerNumber,
        [FromQuery] string? poNumber,
        [FromQuery] string? dateFrom,
        [FromQuery] string? status,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _salesOrderQueryService.SearchSalesOrdersAsync(
            customerNumber,
            poNumber,
            dateFrom,
            status,
            Math.Min(limit, 100),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get Sage-confirmed details for an existing sales order.
    /// </summary>
    [HttpGet("{salesOrderNumber}/details")]
    [RequireApiScope(ApiScopes.Read)]
    public async Task<ActionResult<SalesOrderDetailsResponse>> GetSalesOrderDetails(
        string salesOrderNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNumber))
        {
            return BadRequest(new SalesOrderDetailsResponse
            {
                Success = false,
                ErrorCode = "VALIDATION_ERROR",
                ErrorMessage = "Sales order number is required"
            });
        }

        try
        {
            var result = await _salesOrderService.GetSalesOrderDetailsAsync(salesOrderNumber, cancellationToken);
            if (result.Success)
            {
                return Ok(result);
            }

            return StatusCode(GetStatusCodeForDetailsError(result.ErrorCode), result);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Timeout waiting for available session while reading order details");
            return StatusCode(503, new SalesOrderDetailsResponse
            {
                Success = false,
                SalesOrderNumber = salesOrderNumber,
                ErrorCode = "SERVICE_BUSY",
                ErrorMessage = "Service is busy, please try again"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading sales order details for {OrderNo}", salesOrderNumber);
            return StatusCode(500, new SalesOrderDetailsResponse
            {
                Success = false,
                SalesOrderNumber = salesOrderNumber,
                ErrorCode = "INTERNAL_ERROR",
                ErrorMessage = "Failed to retrieve sales order details"
            });
        }
    }

    /// <summary>
    /// Test endpoint - verifies the API is working (does not create an order)
    /// </summary>
    [HttpGet("test")]
    [RequireApiScope(ApiScopes.Read)]
    public ActionResult<object> Test()
    {
        return Ok(new
        {
            message = "Sales Order API is working",
            timestamp = DateTime.UtcNow,
            endpoints = new
            {
                createOrder = "POST /api/v1/sales-orders",
                health = "GET /health",
                healthReady = "GET /health/ready"
            }
        });
    }

    private ErrorResponse CreateValidationErrorResponse()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => new ValidationError
            {
                Field = x.Key,
                Message = e.ErrorMessage
            }))
            .ToList();

        return new ErrorResponse
        {
            Error = "Validation failed",
            ErrorCode = "VALIDATION_ERROR",
            ValidationErrors = errors
        };
    }

    private static int GetStatusCodeForError(string? errorCode)
    {
        return errorCode switch
        {
            "CUSTOMER_NOT_FOUND" => 400,
            "ITEM_NOT_FOUND" => 400,
            "VALIDATION_ERROR" => 400,
            "COM_ERROR" => 502,
            _ => 500
        };
    }

    private static int GetStatusCodeForDetailsError(string? errorCode)
    {
        return errorCode switch
        {
            "VALIDATION_ERROR" => 400,
            "ORDER_NOT_FOUND" => 404,
            "COM_ERROR" => 502,
            "SERVICE_BUSY" => 503,
            _ => 500
        };
    }
}
