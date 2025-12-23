namespace SageBOI.Api.Services;

public interface IProvideXSessionManager
{
    /// <summary>
    /// Gets an available session from the pool
    /// </summary>
    Task<SessionWrapper> GetSessionAsync();
    
    /// <summary>
    /// Returns a session to the pool
    /// </summary>
    void ReleaseSession(SessionWrapper session);
    
    /// <summary>
    /// Checks if the session manager is healthy and can connect to Sage 100
    /// </summary>
    Task<bool> IsHealthyAsync();
}

public class SessionWrapper
{
    public dynamic ProvideXScript { get; set; } = null!;
    public dynamic Session { get; set; } = null!;
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
}

