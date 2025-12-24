namespace UAFMiddleware.Models;

/// <summary>
/// Request to resolve/identify the correct customer from PO data
/// </summary>
public class CustomerResolutionRequest
{
    /// <summary>
    /// Customer/company name from the PO
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Ship-to address from the PO (primary matching criteria)
    /// </summary>
    public AddressInfo? ShipToAddress { get; set; }
    
    /// <summary>
    /// Billing/business address from the PO
    /// </summary>
    public AddressInfo? BillingAddress { get; set; }
    
    /// <summary>
    /// Phone number from the PO
    /// </summary>
    public string? Phone { get; set; }
    
    /// <summary>
    /// Minimum confidence threshold to auto-accept (default 0.8 = 80%)
    /// </summary>
    public double MinConfidence { get; set; } = 0.8;
}

/// <summary>
/// Address information for matching
/// </summary>
public class AddressInfo
{
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
}

/// <summary>
/// Result of customer resolution
/// </summary>
public class CustomerResolutionResponse
{
    /// <summary>
    /// Whether a confident match was found
    /// </summary>
    public bool Resolved { get; set; }
    
    /// <summary>
    /// Overall confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Recommendation: AUTO_PROCESS, MANUAL_REVIEW, or REJECTED
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
    
    /// <summary>
    /// The best matching customer (if found)
    /// </summary>
    public CustomerMatchResult? BestMatch { get; set; }
    
    /// <summary>
    /// All candidate matches with scores
    /// </summary>
    public List<CustomerMatchResult> Candidates { get; set; } = new();
    
    /// <summary>
    /// Explanation of the resolution
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Reasons for the score/recommendation
    /// </summary>
    public List<string> ScoringDetails { get; set; } = new();
}

/// <summary>
/// A potential customer match with scoring details
/// </summary>
public class CustomerMatchResult
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Overall match score (0-1)
    /// </summary>
    public double Score { get; set; }
    
    /// <summary>
    /// Individual scoring components
    /// </summary>
    public MatchScoreBreakdown ScoreBreakdown { get; set; } = new();
    
    /// <summary>
    /// The ship-to code that matched best
    /// </summary>
    public string? MatchedShipToCode { get; set; }
    
    /// <summary>
    /// Whether this ship-to is the customer's default
    /// </summary>
    public bool IsDefaultShipTo { get; set; }
    
    /// <summary>
    /// Warehouse code for the matched ship-to
    /// </summary>
    public string? WarehouseCode { get; set; }
    
    /// <summary>
    /// Ship via for the matched ship-to
    /// </summary>
    public string? ShipVia { get; set; }
    
    /// <summary>
    /// Customer details
    /// </summary>
    public CustomerDto? CustomerDetails { get; set; }
}

/// <summary>
/// Breakdown of how the match score was calculated
/// </summary>
public class MatchScoreBreakdown
{
    /// <summary>
    /// Score from customer name match (0-1)
    /// </summary>
    public double NameScore { get; set; }
    
    /// <summary>
    /// Score from ship-to address match (0-1)
    /// </summary>
    public double ShipToScore { get; set; }
    
    /// <summary>
    /// Score from billing address match (0-1)
    /// </summary>
    public double BillingScore { get; set; }
    
    /// <summary>
    /// Score from phone number match (0-1)
    /// </summary>
    public double PhoneScore { get; set; }
    
    /// <summary>
    /// Bonus for matching default ship-to
    /// </summary>
    public double DefaultShipToBonus { get; set; }
    
    /// <summary>
    /// Details about each scoring component
    /// </summary>
    public List<string> Details { get; set; } = new();
}

