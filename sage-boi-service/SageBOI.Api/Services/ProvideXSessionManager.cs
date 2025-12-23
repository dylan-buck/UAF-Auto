using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using SageBOI.Api.Configuration;

namespace SageBOI.Api.Services;

public class ProvideXSessionManager : IProvideXSessionManager, IDisposable
{
    private readonly SageConfiguration _config;
    private readonly ILogger<ProvideXSessionManager> _logger;
    private readonly ConcurrentBag<SessionWrapper> _availableSessions;
    private readonly ConcurrentDictionary<string, SessionWrapper> _activeSessions;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public ProvideXSessionManager(
        IOptions<SageConfiguration> config,
        ILogger<ProvideXSessionManager> logger)
    {
        _config = config.Value;
        _logger = logger;
        _availableSessions = new ConcurrentBag<SessionWrapper>();
        _activeSessions = new ConcurrentDictionary<string, SessionWrapper>();
        _semaphore = new SemaphoreSlim(_config.SessionPoolSize, _config.SessionPoolSize);
        
        InitializePool();
    }

    private void InitializePool()
    {
        _logger.LogInformation("Initializing ProvideX session pool with {PoolSize} sessions", 
            _config.SessionPoolSize);
        
        // Pre-create sessions in the pool
        for (int i = 0; i < _config.SessionPoolSize; i++)
        {
            try
            {
                var session = CreateNewSession();
                _availableSessions.Add(session);
                _logger.LogInformation("Created session {SessionId}", session.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session during pool initialization");
            }
        }
    }

    private SessionWrapper CreateNewSession()
    {
        try
        {
            // Create ProvideX.Script COM object
            Type? pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            if (pvxType == null)
            {
                throw new InvalidOperationException("ProvideX.Script COM object not registered");
            }

            dynamic pvx = Activator.CreateInstance(pvxType) 
                ?? throw new InvalidOperationException("Failed to create ProvideX.Script instance");

            // Initialize with server path
            _logger.LogDebug("Initializing ProvideX with path: {ServerPath}", _config.ServerPath);
            pvx.Init(_config.ServerPath);

            // Create session object
            dynamic session = pvx.NewObject("SY_Session");
            if (session == null)
            {
                throw new InvalidOperationException("Failed to create SY_Session object");
            }
            
            // Authenticate
            _logger.LogDebug("Authenticating user: {Username}", _config.Username);
            int userRet = session.nSetUser(_config.Username, _config.Password);
            if (userRet == 0)
            {
                string error = session.sLastErrorMsg;
                throw new InvalidOperationException($"Failed to authenticate user '{_config.Username}': {error}");
            }
            
            // Set company
            _logger.LogDebug("Setting company: {Company}", _config.Company);
            int companyRet = session.nSetCompany(_config.Company);
            if (companyRet == 0)
            {
                string error = session.sLastErrorMsg;
                throw new InvalidOperationException($"Failed to set company '{_config.Company}': {error}");
            }

            // Set Module and Date (Best Practice)
            session.nSetModule("S/O");
            session.nSetDate("S/O", DateTime.Now.ToString("yyyyMMdd"));

            var wrapper = new SessionWrapper
            {
                ProvideXScript = pvx,
                Session = session,
                SessionId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            _logger.LogInformation("Successfully created new session {SessionId}", wrapper.SessionId);
            return wrapper;
        }
        catch (COMException comEx)
        {
            _logger.LogError(comEx, "COM error creating session. HRESULT: {HResult}", comEx.HResult);
            throw new InvalidOperationException($"Failed to create ProvideX session: {comEx.Message}", comEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            throw;
        }
    }

    public async Task<SessionWrapper> GetSessionAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProvideXSessionManager));
        }

        // Wait for an available slot
        await _semaphore.WaitAsync();

        try
        {
            // Try to get an existing session from the pool
            if (_availableSessions.TryTake(out var session))
            {
                session.LastUsed = DateTime.UtcNow;
                _activeSessions.TryAdd(session.SessionId, session);
                _logger.LogDebug("Retrieved session {SessionId} from pool", session.SessionId);
                return session;
            }

            // If no session available, create a new one
            _logger.LogWarning("No sessions available in pool, creating new session");
            var newSession = CreateNewSession();
            _activeSessions.TryAdd(newSession.SessionId, newSession);
            return newSession;
        }
        catch
        {
            // Release the semaphore if we failed to get a session
            _semaphore.Release();
            throw;
        }
    }

    public void ReleaseSession(SessionWrapper session)
    {
        if (session == null)
        {
            return;
        }

        try
        {
            _activeSessions.TryRemove(session.SessionId, out _);
            session.LastUsed = DateTime.UtcNow;
            _availableSessions.Add(session);
            _semaphore.Release();
            _logger.LogDebug("Released session {SessionId} back to pool", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing session {SessionId}", session.SessionId);
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var session = await GetSessionAsync();
            try
            {
                // Try to create a simple object to verify connectivity
                dynamic testObj = session.ProvideXScript.NewObject("SY_Session");
                return true;
            }
            finally
            {
                ReleaseSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        _logger.LogInformation("Disposing ProvideX session manager");

        // Cleanup all sessions
        while (_availableSessions.TryTake(out var session))
        {
            try
            {
                if (session.Session != null && Marshal.IsComObject(session.Session))
                {
                    Marshal.ReleaseComObject(session.Session);
                }
                if (session.ProvideXScript != null && Marshal.IsComObject(session.ProvideXScript))
                {
                    Marshal.ReleaseComObject(session.ProvideXScript);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing session {SessionId}", session.SessionId);
            }
        }

        foreach (var session in _activeSessions.Values)
        {
            try
            {
                if (session.Session != null && Marshal.IsComObject(session.Session))
                {
                    Marshal.ReleaseComObject(session.Session);
                }
                if (session.ProvideXScript != null && Marshal.IsComObject(session.ProvideXScript))
                {
                    Marshal.ReleaseComObject(session.ProvideXScript);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing active session {SessionId}", session.SessionId);
            }
        }

        _semaphore?.Dispose();
    }
}

