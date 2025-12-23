namespace SageBOI.Api.Models;

public class SalesOrderDTO
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string? ShipDate { get; set; }
    public string? Comment { get; set; }
    public AddressDTO? ShipToAddress { get; set; }
    public List<SalesOrderLineDTO> Lines { get; set; } = new();
}

