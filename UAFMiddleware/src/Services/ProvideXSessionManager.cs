using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using UAFMiddleware.Configuration;

namespace UAFMiddleware.Services;

public class ProvideXSessionManager : IProvideXSessionManager, IDisposable
{
    private readonly SageConfiguration _config;
    private readonly ILogger<ProvideXSessionManager> _logger;
    private readonly ConcurrentBag<SessionWrapper> _availableSessions;
    private readonly ConcurrentDictionary<string, SessionWrapper> _activeSessions;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;
    private bool _initialized;

    public int AvailableSessions => _availableSessions.Count;
    public int ActiveSessions => _activeSessions.Count;

    public ProvideXSessionManager(
        IOptions<SageConfiguration> config,
        ILogger<ProvideXSessionManager> logger)
    {
        _config = config.Value;
        _logger = logger;
        _availableSessions = new ConcurrentBag<SessionWrapper>();
        _activeSessions = new ConcurrentDictionary<string, SessionWrapper>();
        _semaphore = new SemaphoreSlim(_config.SessionPoolSize, _config.SessionPoolSize);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            _logger.LogDebug("Already initialized, skipping");
            return;
        }

        _logger.LogDebug("Acquiring initialization lock...");
        lock (this)
        {
            if (_initialized)
            {
                _logger.LogDebug("Already initialized (inside lock), skipping");
                return;
            }

            _logger.LogInformation("Initializing ProvideX session pool with {PoolSize} sessions", 
                _config.SessionPoolSize);
            _logger.LogInformation("Sage 100 Server Path: {ServerPath}", _config.ServerPath);
            _logger.LogInformation("Sage 100 Company: {Company}", _config.Company);
            
            int successCount = 0;
            // Pre-create sessions in the pool
            for (int i = 0; i < _config.SessionPoolSize; i++)
            {
                try
                {
                    _logger.LogInformation("Creating session {Index}/{Total}...", i + 1, _config.SessionPoolSize);
                    var session = CreateNewSession();
                    _availableSessions.Add(session);
                    successCount++;
                    _logger.LogInformation("Created session {Index}/{Total}: {SessionId}. Pool now has {Count} sessions.", 
                        i + 1, _config.SessionPoolSize, session.SessionId, _availableSessions.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create session {Index} during pool initialization", i + 1);
                }
            }

            _initialized = true;
            _logger.LogInformation("Session pool initialized. Created {Success}/{Total} sessions. Pool count: {Count}", 
                successCount, _config.SessionPoolSize, _availableSessions.Count);
        }
    }

    private SessionWrapper CreateNewSession()
    {
        try
        {
            // Create ProvideX.Script COM object
            _logger.LogInformation("Creating ProvideX.Script COM object...");
            Type? pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            if (pvxType == null)
            {
                throw new InvalidOperationException(
                    "ProvideX.Script COM object not registered. " +
                    "Ensure Sage 100 workstation components are installed.");
            }
            _logger.LogDebug("ProvideX.Script type: {TypeName}", pvxType.FullName);

            dynamic pvx = Activator.CreateInstance(pvxType) 
                ?? throw new InvalidOperationException("Failed to create ProvideX.Script instance");
            _logger.LogDebug("ProvideX.Script instance created");

            // Initialize with server path
            _logger.LogInformation("Initializing ProvideX with path: {ServerPath}", _config.ServerPath);
            pvx.Init(_config.ServerPath);
            _logger.LogDebug("ProvideX.Init completed");

            // Create session object
            _logger.LogInformation("Creating SY_Session object...");
            dynamic session = pvx.NewObject("SY_Session");
            if (session == null)
            {
                throw new InvalidOperationException("Failed to create SY_Session object - NewObject returned null");
            }
            
            // Verify the session object has the expected methods
            string sessionTypeName = ((object)session).GetType().ToString();
            _logger.LogDebug("SY_Session object created, type: {Type}", sessionTypeName);
            
            // Authenticate - use a variable to help debug
            _logger.LogInformation("Authenticating user: {Username}", _config.Username);
            object userRetObj;
            try
            {
                userRetObj = session.nSetUser(_config.Username, _config.Password);
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "nSetUser COM error. Session type: {Type}", sessionTypeName);
                throw;
            }
            
            int userRet = userRetObj != null ? Convert.ToInt32(userRetObj) : 0;
            _logger.LogInformation("nSetUser returned: {Result}", userRet);
            
            if (userRet == 0)
            {
                string error = "";
                try { error = session.sLastErrorMsg ?? "Unknown error"; } catch { error = "Could not get error message"; }
                throw new InvalidOperationException($"Failed to authenticate user '{_config.Username}': {error}");
            }
            
            // Set company
            _logger.LogInformation("Setting company: {Company}", _config.Company);
            object companyRetObj;
            try
            {
                companyRetObj = session.nSetCompany(_config.Company);
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "nSetCompany COM error");
                throw;
            }
            
            int companyRet = companyRetObj != null ? Convert.ToInt32(companyRetObj) : 0;
            _logger.LogInformation("nSetCompany returned: {Result}", companyRet);
            
            if (companyRet == 0)
            {
                string error = "";
                try { error = session.sLastErrorMsg ?? "Unknown error"; } catch { error = "Could not get error message"; }
                throw new InvalidOperationException($"Failed to set company '{_config.Company}': {error}");
            }

            // Set Module and Date context (best practice)
            try
            {
                session.nSetModule("S/O");
                session.nSetDate("S/O", DateTime.Now.ToString("yyyyMMdd"));
                _logger.LogDebug("Set module and date context");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set module/date context (non-fatal)");
            }

            var wrapper = new SessionWrapper
            {
                ProvideXScript = pvx,
                Session = session,
                SessionId = Guid.NewGuid().ToString()[..8],
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };

            _logger.LogInformation("Successfully created new session {SessionId}", wrapper.SessionId);
            return wrapper;
        }
        catch (COMException comEx)
        {
            _logger.LogError(comEx, "COM error creating session. HRESULT: 0x{HResult:X}", comEx.HResult);
            throw new InvalidOperationException($"Failed to create ProvideX session: {comEx.Message}", comEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            throw;
        }
    }

    public async Task<SessionWrapper> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProvideXSessionManager));
        }

        _logger.LogDebug("GetSessionAsync called. Available: {Available}, Active: {Active}", 
            _availableSessions.Count, _activeSessions.Count);

        EnsureInitialized();

        _logger.LogDebug("After EnsureInitialized. Available: {Available}, Active: {Active}", 
            _availableSessions.Count, _activeSessions.Count);

        // Wait for an available slot (with timeout)
        _logger.LogDebug("Waiting for semaphore...");
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            throw new TimeoutException("Timeout waiting for available session");
        }
        _logger.LogDebug("Semaphore acquired. Trying to get session from pool...");

        try
        {
            // Try to get an existing session from the pool
            if (_availableSessions.TryTake(out var session))
            {
                session.LastUsed = DateTime.UtcNow;
                _activeSessions.TryAdd(session.SessionId, session);
                _logger.LogInformation("Retrieved session {SessionId} from pool. Available: {Available}, Active: {Active}", 
                    session.SessionId, _availableSessions.Count, _activeSessions.Count);
                return session;
            }

            // If no session available, create a new one
            _logger.LogWarning("No sessions available in pool (Available: {Available}, Active: {Active}), creating new session",
                _availableSessions.Count, _activeSessions.Count);
            var newSession = CreateNewSession();
            _activeSessions.TryAdd(newSession.SessionId, newSession);
            return newSession;
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    public void ReleaseSession(SessionWrapper session)
    {
        if (session == null) return;

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

    public void InvalidateSession(SessionWrapper session)
    {
        if (session == null) return;

        try
        {
            _activeSessions.TryRemove(session.SessionId, out _);
            _logger.LogWarning("Invalidating session {SessionId} - will be disposed and not returned to pool", session.SessionId);
            
            // Dispose the session
            DisposeSession(session);
            
            // Release semaphore but don't add session back to pool
            // A new session will be created lazily on the next GetSessionAsync call
            _semaphore.Release();
            
            _logger.LogInformation("Session {SessionId} invalidated. A new session will be created on next request.", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating session {SessionId}", session.SessionId);
            // Still release the semaphore to prevent deadlocks
            try { _semaphore.Release(); } catch { }
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            EnsureInitialized();
            
            if (_availableSessions.IsEmpty && _activeSessions.IsEmpty)
            {
                _logger.LogWarning("No sessions in pool");
                return false;
            }

            // Try to get and release a session
            var session = await GetSessionAsync();
            try
            {
                // Simple test - the session exists and is valid
                return session.Session != null;
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
        if (_disposed) return;
        _disposed = true;
        
        _logger.LogInformation("Disposing ProvideX session manager");

        // Cleanup all available sessions
        while (_availableSessions.TryTake(out var session))
        {
            DisposeSession(session);
        }

        // Cleanup active sessions
        foreach (var session in _activeSessions.Values)
        {
            DisposeSession(session);
        }
        _activeSessions.Clear();

        _semaphore?.Dispose();
    }

    private void DisposeSession(SessionWrapper session)
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
            _logger.LogDebug("Disposed session {SessionId}", session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing session {SessionId}", session.SessionId);
        }
    }
}

