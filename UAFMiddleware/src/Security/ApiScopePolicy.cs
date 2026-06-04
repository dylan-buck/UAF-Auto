using UAFMiddleware.Configuration;

namespace UAFMiddleware.Security;

public static class ApiScopes
{
    public const string Read = "read";
    public const string Create = "create";
    public const string Modify = "modify";
    public const string Finance = "finance";
    public const string Admin = "admin";

    public static readonly IReadOnlyCollection<string> WritableDefault =
    [
        Read,
        Create,
        Modify
    ];
}

public sealed class ApiScopeResult
{
    public bool IsAuthenticated { get; init; }
    public string? ClientName { get; init; }
    public HashSet<string> Scopes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasScope(string scope) => Scopes.Contains(scope);
}

public static class ApiScopePolicy
{
    public static ApiScopeResult ResolveScopes(ApiConfiguration? config, string? providedKey)
    {
        if (config == null || string.IsNullOrWhiteSpace(providedKey))
        {
            return new ApiScopeResult { IsAuthenticated = false };
        }

        ApiScopeResult result;

        if (config.ApiKeys?.TryGetValue(providedKey, out var client) == true)
        {
            result = new ApiScopeResult
            {
                IsAuthenticated = true,
                ClientName = client.Name,
                Scopes = NormalizeScopes(client.Scopes)
            };
        }
        else if (!string.IsNullOrWhiteSpace(config.ApiKey) && providedKey == config.ApiKey)
        {
            result = new ApiScopeResult
            {
                IsAuthenticated = true,
                ClientName = "legacy",
                Scopes = NormalizeScopes(ApiScopes.WritableDefault)
            };
        }
        else
        {
            return new ApiScopeResult { IsAuthenticated = false };
        }

        if (config.ReadOnlyMode)
        {
            result.Scopes.RemoveWhere(scope => !string.Equals(scope, ApiScopes.Read, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static HashSet<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in scopes ?? [])
        {
            if (!string.IsNullOrWhiteSpace(scope))
            {
                normalized.Add(scope.Trim().ToLowerInvariant());
            }
        }

        return normalized;
    }
}
