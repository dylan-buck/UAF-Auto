namespace SageBOI.Api.Models;

public class BOIResult
{
    public bool Success { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}

