using System.ComponentModel.DataAnnotations;

namespace UAFMiddleware.Models;

public class SalesOrderRequest
{
    [Required(ErrorMessage = "Customer number is required")]
    public string CustomerNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "PO number is required")]
    public string PONumber { get; set; } = string.Empty;

    public string? OrderDate { get; set; }

    public string? ShipDate { get; set; }

    public string? Comment { get; set; }

    public string? ARDivisionNo { get; set; } = "00";

    /// <summary>
    /// Ship-to code from customer resolution (e.g., "001", "MAIN")
    /// When set, this will use the customer's existing ship-to address from Sage
    /// </summary>
    public string? ShipToCode { get; set; }

    /// <summary>
    /// Warehouse code from the matched ship-to address
    /// </summary>
    public string? WarehouseCode { get; set; }

    /// <summary>
    /// Ship via method from the matched ship-to address
    /// </summary>
    public string? ShipVia { get; set; }

    /// <summary>
    /// Override ship-to address (optional - used if ShipToCode is not provided)
    /// </summary>
    public ShipToAddress? ShipToAddress { get; set; }

    [Required(ErrorMessage = "At least one line item is required")]
    [MinLength(1, ErrorMessage = "At least one line item is required")]
    public List<SalesOrderLine> Lines { get; set; } = new();
}

public class SalesOrderLine
{
    [Required(ErrorMessage = "Item code is required")]
    public string ItemCode { get; set; } = string.Empty;
    
    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; }
    
    public decimal? UnitPrice { get; set; }
    
    public string? Description { get; set; }
    
    public string? WarehouseCode { get; set; }
}

public class ShipToAddress
{
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
}


