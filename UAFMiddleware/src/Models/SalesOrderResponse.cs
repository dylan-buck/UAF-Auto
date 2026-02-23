namespace UAFMiddleware.Models;

public class SalesOrderResponse
{
    public bool Success { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? InvalidItems { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SalesOrderDetailsResponse
{
    public bool Success { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? CustomerNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? ShipToCode { get; set; }
    public string? WarehouseCode { get; set; }
    public string? ShipVia { get; set; }
    public decimal? OrderTotal { get; set; }
    public List<SalesOrderLineDetail> Lines { get; set; } = new();
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SalesOrderLineDetail
{
    public int LineNumber { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? ExtendedPrice { get; set; }
    public string? WarehouseCode { get; set; }
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

