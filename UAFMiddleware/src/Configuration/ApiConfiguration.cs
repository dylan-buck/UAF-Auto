namespace UAFMiddleware.Configuration;

public class ApiConfiguration
{
    public int Port { get; set; } = 3000;
    public string ApiKey { get; set; } = string.Empty;
    public bool ReadOnlyMode { get; set; }
    public Dictionary<string, ApiKeyClient> ApiKeys { get; set; } = new();
}

public class ApiKeyClient
{
    public string? Name { get; set; }
    public List<string> Scopes { get; set; } = new();
}

