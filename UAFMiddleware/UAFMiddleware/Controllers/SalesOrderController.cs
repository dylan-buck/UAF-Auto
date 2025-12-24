using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

[ApiController]
[Route("api/v1/sales-orders")]
public class SalesOrderController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;
    private readonly ILogger<SalesOrderController> _logger;

    public SalesOrderController(
        ISalesOrderService salesOrderService,
        ILogger<SalesOrderController> logger)
    {
        _salesOrderService = salesOrderService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sales order in Sage 100
    /// </summary>
    [HttpPost]
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
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => new ValidationError
                {
                    Field = x.Key,
                    Message = e.ErrorMessage
                }))
                .ToList();

            return BadRequest(new ErrorResponse
            {
                Error = "Validation failed",
                ErrorCode = "VALIDATION_ERROR",
                ValidationErrors = errors
            });
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
                
                // Determine appropriate status code
                var statusCode = result.ErrorCode switch
                {
                    "CUSTOMER_NOT_FOUND" => 400,
                    "ITEM_NOT_FOUND" => 400,
                    "VALIDATION_ERROR" => 400,
                    "COM_ERROR" => 502,
                    _ => 500
                };

                return StatusCode(statusCode, result);
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
                ErrorMessage = "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Test endpoint - verifies the API is working (does not create an order)
    /// </summary>
    [HttpGet("test")]
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
}



