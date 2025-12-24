namespace UAFMiddleware.Models;

/// <summary>
/// Customer search result
/// </summary>
public class CustomerDto
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string ARDivisionNo { get; set; } = string.Empty;
    public string CustomerNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Status { get; set; }
    
    // Billing Address
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    
    // Defaults
    public string? PriceLevel { get; set; }
    public string? TaxSchedule { get; set; }
    public string? TermsCode { get; set; }
    
    /// <summary>
    /// The default/primary ship-to code from the customer record
    /// </summary>
    public string? DefaultShipToCode { get; set; }
    
    // Default Ship-To (if available)
    public CustomerShipToDto? DefaultShipTo { get; set; }
    
    // All Ship-To Addresses
    public List<CustomerShipToDto> ShipToAddresses { get; set; } = new();
}

/// <summary>
/// Ship-to address for a customer
/// </summary>
public class CustomerShipToDto
{
    public string ShipToCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    
    // Defaults for this ship-to
    public string? WarehouseCode { get; set; }
    public string? ShipVia { get; set; }
    
    public bool IsDefault { get; set; }
}

/// <summary>
/// Customer search request parameters
/// </summary>
public class CustomerSearchRequest
{
    /// <summary>
    /// Customer name to search for (partial match)
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Address line to match
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// City to filter by
    /// </summary>
    public string? City { get; set; }
    
    /// <summary>
    /// State to filter by
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// Phone number to match
    /// </summary>
    public string? Phone { get; set; }
    
    /// <summary>
    /// Maximum results to return (default 20)
    /// </summary>
    public int Limit { get; set; } = 20;
}

/// <summary>
/// Customer search response
/// </summary>
public class CustomerSearchResponse
{
    public List<CustomerDto> Customers { get; set; } = new();
    public int TotalCount { get; set; }
    public string? SearchCriteria { get; set; }
}

/// <summary>
/// Ship-to validation request
/// </summary>
public class ValidateShipToRequest
{
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}

/// <summary>
/// Ship-to validation response
/// </summary>
public class ValidateShipToResponse
{
    public bool Matched { get; set; }
    public bool IsDefaultShipTo { get; set; }
    public string? MatchedShipToCode { get; set; }
    public string? WarehouseCode { get; set; }
    public string? ShipVia { get; set; }
    public double MatchConfidence { get; set; }
    public CustomerShipToDto? MatchedAddress { get; set; }
    public List<string>? Differences { get; set; }
}

