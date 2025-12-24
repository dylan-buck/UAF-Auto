using Microsoft.AspNetCore.Mvc;
using UAFMiddleware.Models;
using UAFMiddleware.Services;

namespace UAFMiddleware.Controllers;

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
    /// Basic health check - is the API running?
    /// </summary>
    [HttpGet]
    [HttpGet("/health")]
    public ActionResult<HealthResponse> GetHealth()
    {
        var uptime = DateTime.UtcNow - HealthMonitorService.StartTime;
        
        return Ok(new HealthResponse
        {
            Status = "healthy",
            Version = "1.0.0",
            Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Detailed health check - includes Sage 100 connectivity
    /// </summary>
    [HttpGet("ready")]
    [HttpGet("/health/ready")]
    public async Task<ActionResult<HealthResponse>> GetReadiness()
    {
        try
        {
            var sage100Healthy = await _sessionManager.IsHealthyAsync();
            var uptime = DateTime.UtcNow - HealthMonitorService.StartTime;
            
            var response = new HealthResponse
            {
                Status = sage100Healthy ? "ready" : "degraded",
                Sage100 = sage100Healthy ? "connected" : "disconnected",
                Version = "1.0.0",
                Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m",
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    { "availableSessions", _sessionManager.AvailableSessions },
                    { "activeSessions", _sessionManager.ActiveSessions }
                }
            };

            if (!sage100Healthy)
            {
                return StatusCode(503, response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new HealthResponse
            {
                Status = "unhealthy",
                Sage100 = "error",
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    { "error", ex.Message }
                }
            });
        }
    }
}



