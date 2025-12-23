using Microsoft.AspNetCore.Mvc;
using SageBOI.Api.Models;
using SageBOI.Api.Services;

namespace SageBOI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(typeof(BOIResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BOIResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BOIResult>> CreateSalesOrder([FromBody] SalesOrderDTO order)
    {
        try
        {
            if (order == null)
            {
                return BadRequest(new BOIResult
                {
                    Success = false,
                    ErrorCode = "INVALID_REQUEST",
                    ErrorMessage = "Order data is required"
                });
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(order.CustomerNumber))
            {
                return BadRequest(new BOIResult
                {
                    Success = false,
                    ErrorCode = "MISSING_CUSTOMER",
                    ErrorMessage = "Customer number is required"
                });
            }

            if (order.Lines == null || !order.Lines.Any())
            {
                return BadRequest(new BOIResult
                {
                    Success = false,
                    ErrorCode = "MISSING_LINES",
                    ErrorMessage = "At least one line item is required"
                });
            }

            _logger.LogInformation("Received request to create sales order for customer {Customer}", 
                order.CustomerNumber);

            var result = await _salesOrderService.CreateSalesOrderAsync(order);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sales order request");
            return StatusCode(500, new BOIResult
            {
                Success = false,
                ErrorCode = "INTERNAL_ERROR",
                ErrorMessage = "An internal error occurred processing your request"
            });
        }
    }
}

