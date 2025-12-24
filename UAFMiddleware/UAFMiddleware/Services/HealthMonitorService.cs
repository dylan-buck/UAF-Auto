namespace UAFMiddleware.Services;

/// <summary>
/// Background service that monitors Sage 100 connectivity
/// and logs periodic health status
/// </summary>
public class HealthMonitorService : BackgroundService
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<HealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private DateTime _startTime;

    public HealthMonitorService(
        IProvideXSessionManager sessionManager,
        ILogger<HealthMonitorService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public static DateTime StartTime { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startTime = DateTime.UtcNow;
        StartTime = _startTime;
        
        _logger.LogInformation("Health monitor service started");

        // Initial delay to let everything start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var isHealthy = await _sessionManager.IsHealthyAsync();
                var uptime = DateTime.UtcNow - _startTime;
                
                if (isHealthy)
                {
                    _logger.LogInformation(
                        "Health check passed. Uptime: {Uptime:dd\\.hh\\:mm\\:ss}, Available sessions: {Available}, Active sessions: {Active}",
                        uptime,
                        _sessionManager.AvailableSessions,
                        _sessionManager.ActiveSessions);
                }
                else
                {
                    _logger.LogWarning(
                        "Health check FAILED. Sage 100 connectivity may be degraded. Uptime: {Uptime:dd\\.hh\\:mm\\:ss}",
                        uptime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Health monitor service stopped");
    }
}



