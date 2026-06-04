using System.Runtime.InteropServices;

namespace UAFMiddleware.Services;

public abstract class SageReadServiceBase
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger _logger;

    protected SageReadServiceBase(IProvideXSessionManager sessionManager, ILogger logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    protected async Task<T> WithSageObjectAsync<T>(
        string objectName,
        Func<dynamic, T> action,
        CancellationToken cancellationToken = default)
    {
        SessionWrapper? session = null;
        dynamic? sageObject = null;
        bool sessionCorrupted = false;

        try
        {
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            sageObject = session.ProvideXScript.NewObject(objectName, session.Session);
            if (sageObject == null)
            {
                sessionCorrupted = true;
                throw new InvalidOperationException($"Failed to create {objectName}");
            }

            return action(sageObject);
        }
        catch (COMException)
        {
            sessionCorrupted = true;
            throw;
        }
        finally
        {
            ReleaseComObject(sageObject);

            if (session != null)
            {
                if (sessionCorrupted)
                {
                    _sessionManager.InvalidateSession(session);
                }
                else
                {
                    _sessionManager.ReleaseSession(session);
                }
            }
        }
    }

    protected static int ConvertComResult(object? result)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.ToString()))
        {
            return 0;
        }

        return Convert.ToInt32(result);
    }

    protected static bool TryFind(dynamic obj, params (string Field, string Value)[] keys)
    {
        try
        {
            foreach (var (field, value) in keys)
            {
                obj.nSetKeyValue(field, value);
            }

            return ConvertComResult(obj.nFind()) == 1;
        }
        catch
        {
            try
            {
                return ConvertComResult(obj.nSetKey()) == 1;
            }
            catch
            {
                return false;
            }
        }
    }

    protected static string GetStringValue(dynamic obj, string fieldName)
    {
        try
        {
            string value = "";
            obj.nGetValue(fieldName, ref value);
            return value ?? "";
        }
        catch
        {
            return "";
        }
    }

    protected static decimal? GetDecimalValue(dynamic obj, string fieldName)
    {
        try
        {
            string value = "";
            obj.nGetValue(fieldName, ref value);
            if (decimal.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    protected static bool? GetBooleanValue(dynamic obj, string fieldName)
    {
        var value = GetStringValue(obj, fieldName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    protected static bool MoveFirst(dynamic obj) => ConvertComResult(obj.nMoveFirst()) == 1;
    protected static bool MoveNext(dynamic obj) => ConvertComResult(obj.nMoveNext()) == 1;

    protected static void ReleaseComObject(dynamic? obj)
    {
        try
        {
            if (obj != null && Marshal.IsComObject(obj))
            {
                Marshal.ReleaseComObject(obj);
            }
        }
        catch
        {
            // best-effort COM cleanup
        }
    }

    protected void LogScanLimit(string objectName, int scanned, int limit)
    {
        if (scanned >= limit)
        {
            _logger.LogWarning("Stopped scanning {ObjectName} after {Limit} records", objectName, limit);
        }
    }
}
