namespace SageBOI.Api.Configuration;

public class SageConfiguration
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string ServerPath { get; set; } = string.Empty;
    public int SessionPoolSize { get; set; } = 3;
    public int SessionTimeout { get; set; } = 300;
}

