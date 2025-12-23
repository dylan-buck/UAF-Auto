namespace UAFMiddleware.Models;

public class SalesOrderResponse
{
    public bool Success { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public string? Sage100 { get; set; }
    public string? Version { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Uptime { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Error { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public List<ValidationError>? ValidationErrors { get; set; }
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

