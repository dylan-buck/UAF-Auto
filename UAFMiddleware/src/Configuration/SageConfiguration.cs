namespace UAFMiddleware.Configuration;

public class SageConfiguration
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string ServerPath { get; set; } = string.Empty;
    public int SessionPoolSize { get; set; } = 1;  // Sage 100 BOI typically only supports 1 session per process
    public int SessionTimeoutSeconds { get; set; } = 300;
}

