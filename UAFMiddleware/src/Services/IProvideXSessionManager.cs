namespace UAFMiddleware.Services;

public interface IProvideXSessionManager
{
    Task<SessionWrapper> GetSessionAsync(CancellationToken cancellationToken = default);
    void ReleaseSession(SessionWrapper session);
    Task<bool> IsHealthyAsync();
    int AvailableSessions { get; }
    int ActiveSessions { get; }
}

public class SessionWrapper
{
    public required dynamic ProvideXScript { get; set; }
    public required dynamic Session { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
}

