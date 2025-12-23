using Microsoft.AspNetCore.Mvc;
using SageBOI.Api.Services;

namespace SageBOI.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IProvideXSessionManager sessionManager,
        ILogger<HealthController> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            service = "sage-boi-service",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Detailed health check including Sage 100 connectivity
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        try
        {
            var isHealthy = await _sessionManager.IsHealthyAsync();
            
            if (isHealthy)
            {
                return Ok(new
                {
                    status = "ready",
                    sage100 = "connected",
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(503, new
                {
                    status = "not_ready",
                    sage100 = "unavailable",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new
            {
                status = "not_ready",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

