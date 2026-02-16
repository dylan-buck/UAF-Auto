namespace UAFMiddleware.Services;

/// <summary>
/// Background service that monitors Sage 100 connectivity,
/// logs periodic health status, and triggers self-healing
/// when consecutive failures are detected.
/// </summary>
public class HealthMonitorService : BackgroundService
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<HealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private DateTime _startTime;

    private const int PoolResetThreshold = 3;
    private const int SelfRestartThreshold = 6;

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

        _logger.LogInformation("Health monitor service started (pool reset after {ResetThreshold} failures, self-restart after {RestartThreshold})",
            PoolResetThreshold, SelfRestartThreshold);

        // Initial delay to let everything start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var isHealthy = await _sessionManager.IsHealthyAsync();
                var uptime = DateTime.UtcNow - _startTime;
                var failures = _sessionManager.ConsecutiveHealthFailures;

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
                        "Health check FAILED (consecutive: {Failures}). Uptime: {Uptime:dd\\.hh\\:mm\\:ss}",
                        failures, uptime);

                    // Tier 1: Reset session pool
                    if (failures >= PoolResetThreshold && failures < SelfRestartThreshold)
                    {
                        _logger.LogWarning("Consecutive failures ({Failures}) reached pool reset threshold — resetting session pool", failures);
                        try
                        {
                            _sessionManager.ResetPool();
                            _logger.LogInformation("Session pool reset triggered. Waiting for next health check to verify recovery.");
                        }
                        catch (Exception resetEx)
                        {
                            _logger.LogError(resetEx, "Failed to reset session pool");
                        }
                    }

                    // Tier 2: Graceful self-restart (Windows service recovery will restart the process)
                    if (failures >= SelfRestartThreshold)
                    {
                        _logger.LogCritical(
                            "Consecutive failures ({Failures}) reached self-restart threshold — shutting down for Windows service recovery to restart",
                            failures);

                        // Give logs time to flush
                        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

                        // Exit with non-zero code so Windows service recovery kicks in
                        Environment.Exit(1);
                    }
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
