using UAFMiddleware.Configuration;
using UAFMiddleware.Security;
using Xunit;

namespace UAFMiddleware.Tests;

public class ApiScopePolicyTests
{
    [Fact]
    public void ResolveScopes_legacyApiKeyGetsWritableScopes()
    {
        var config = new ApiConfiguration { ApiKey = "legacy-key" };

        var result = ApiScopePolicy.ResolveScopes(config, "legacy-key");

        Assert.True(result.IsAuthenticated);
        Assert.Contains(ApiScopes.Read, result.Scopes);
        Assert.Contains(ApiScopes.Create, result.Scopes);
        Assert.Contains(ApiScopes.Modify, result.Scopes);
    }

    [Fact]
    public void ResolveScopes_readOnlyClientAllowsReadOnly()
    {
        var config = new ApiConfiguration
        {
            ApiKeys = new Dictionary<string, ApiKeyClient>
            {
                ["readonly-key"] = new() { Scopes = new List<string> { ApiScopes.Read } }
            }
        };

        var result = ApiScopePolicy.ResolveScopes(config, "readonly-key");

        Assert.True(result.IsAuthenticated);
        Assert.True(result.HasScope(ApiScopes.Read));
        Assert.False(result.HasScope(ApiScopes.Create));
        Assert.False(result.HasScope(ApiScopes.Modify));
    }

    [Fact]
    public void ResolveScopes_readOnlyModeRemovesNonReadScopes()
    {
        var config = new ApiConfiguration
        {
            ReadOnlyMode = true,
            ApiKeys = new Dictionary<string, ApiKeyClient>
            {
                ["full-key"] = new()
                {
                    Scopes = new List<string>
                    {
                        ApiScopes.Read,
                        ApiScopes.Create,
                        ApiScopes.Modify,
                        ApiScopes.Finance
                    }
                }
            }
        };

        var result = ApiScopePolicy.ResolveScopes(config, "full-key");

        Assert.True(result.IsAuthenticated);
        Assert.True(result.HasScope(ApiScopes.Read));
        Assert.False(result.HasScope(ApiScopes.Create));
        Assert.False(result.HasScope(ApiScopes.Modify));
        Assert.False(result.HasScope(ApiScopes.Finance));
    }

    [Fact]
    public void ResolveScopes_financeScopeDoesNotImplyReadScope()
    {
        var config = new ApiConfiguration
        {
            ApiKeys = new Dictionary<string, ApiKeyClient>
            {
                ["finance-only-key"] = new() { Scopes = new List<string> { ApiScopes.Finance } }
            }
        };

        var result = ApiScopePolicy.ResolveScopes(config, "finance-only-key");

        Assert.True(result.IsAuthenticated);
        Assert.True(result.HasScope(ApiScopes.Finance));
        Assert.False(result.HasScope(ApiScopes.Read));
    }
}
