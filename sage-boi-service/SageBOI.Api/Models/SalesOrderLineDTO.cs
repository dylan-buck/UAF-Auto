namespace SageBOI.Api.Models;

public class SalesOrderLineDTO
{
    public string ItemCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Description { get; set; }
    public string? WarehouseCode { get; set; }
}

