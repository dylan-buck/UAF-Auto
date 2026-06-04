namespace UAFMiddleware.Models;

public class ItemDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductLine { get; set; }
    public string? ItemType { get; set; }
    public string? StandardUnitOfMeasure { get; set; }
    public string? SalesUnitOfMeasure { get; set; }
    public string? PurchaseUnitOfMeasure { get; set; }
    public string? UpcCode { get; set; }
    public string? EanCode { get; set; }
    public bool? Inactive { get; set; }
}

public class ItemSearchResponse
{
    public List<ItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ScannedCount { get; set; }
}

public class ItemAvailabilityRequest
{
    public List<ItemAvailabilityRequestLine> Items { get; set; } = new();
}

public class ItemAvailabilityRequestLine
{
    public string ItemCode { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
}

public class ItemAvailabilityResponse
{
    public List<ItemAvailabilityDto> Items { get; set; } = new();
}

public class ItemAvailabilityDto
{
    public string ItemCode { get; set; } = string.Empty;
    public List<ItemWarehouseAvailabilityDto> Warehouses { get; set; } = new();
}

public class ItemWarehouseAvailabilityDto
{
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal? QuantityOnHand { get; set; }
    public decimal? QuantityAvailable { get; set; }
    public decimal? QuantityCommitted { get; set; }
    public decimal? QuantityOnPurchaseOrder { get; set; }
    public decimal? QuantityOnSalesOrder { get; set; }
}

public class ItemRelatedItemsResponse
{
    public string ItemCode { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public List<ItemRelatedItemDto> Items { get; set; } = new();
}

public class ItemRelatedItemDto
{
    public string? ItemCode { get; set; }
    public string? RelatedItemCode { get; set; }
    public string? Description { get; set; }
    public string? RelationshipCode { get; set; }
}

public class SalesOrderSearchResponse
{
    public List<SalesOrderSummaryDto> SalesOrders { get; set; } = new();
    public int TotalCount { get; set; }
    public int ScannedCount { get; set; }
}

public class SalesOrderSummaryDto
{
    public string SalesOrderNumber { get; set; } = string.Empty;
    public string? CustomerNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? OrderDate { get; set; }
    public string? ShipExpireDate { get; set; }
    public string? OrderStatus { get; set; }
    public decimal? OrderTotal { get; set; }
}

public class CustomerAccountSummaryResponse
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? Status { get; set; }
    public string? CreditHold { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? CurrentBalance { get; set; }
    public string? LastInvoiceDate { get; set; }
    public decimal? LastInvoiceAmount { get; set; }
    public int OpenInvoiceCount { get; set; }
    public decimal? OpenInvoiceBalance { get; set; }
    public List<OpenInvoiceSummaryDto> OpenInvoices { get; set; } = new();
}

public class OpenInvoiceSummaryDto
{
    public string? InvoiceNo { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public decimal? Balance { get; set; }
    public decimal? InvoiceAmount { get; set; }
}

public class VendorDto
{
    public string VendorNumber { get; set; } = string.Empty;
    public string? APDivisionNo { get; set; }
    public string? VendorNo { get; set; }
    public string? VendorName { get; set; }
    public string? Status { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? TermsCode { get; set; }
}

public class VendorSearchResponse
{
    public List<VendorDto> Vendors { get; set; } = new();
    public int TotalCount { get; set; }
    public int ScannedCount { get; set; }
}

public class PurchaseOrderDto
{
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string? OrderType { get; set; }
    public string? OrderStatus { get; set; }
    public string? VendorNumber { get; set; }
    public string? VendorName { get; set; }
    public string? OrderDate { get; set; }
    public string? RequiredExpireDate { get; set; }
    public decimal? OrderTotal { get; set; }
}

public class PurchaseOrderSearchResponse
{
    public List<PurchaseOrderDto> PurchaseOrders { get; set; } = new();
    public int TotalCount { get; set; }
    public int ScannedCount { get; set; }
}

public class ReferenceDataResponse
{
    public string Type { get; set; } = string.Empty;
    public List<ReferenceDataItemDto> Items { get; set; } = new();
}

public class ReferenceDataItemDto
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
