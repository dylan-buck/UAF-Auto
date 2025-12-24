using System.Runtime.InteropServices;
using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public class CustomerService : ICustomerService
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IProvideXSessionManager sessionManager,
        ILogger<CustomerService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<CustomerSearchResponse> SearchCustomersAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        SessionWrapper? session = null;
        dynamic? customerSvc = null;
        var customers = new List<CustomerDto>();
        
        try
        {
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            _logger.LogInformation("Searching customers: Name={Name}, City={City}, State={State}", 
                request.Name, request.City, request.State);

            // Create AR_Customer_svc object for reading customer data
            customerSvc = session.ProvideXScript.NewObject("AR_Customer_svc", session.Session);
            
            if (customerSvc == null)
            {
                throw new InvalidOperationException("Failed to create AR_Customer_svc object");
            }

            // Build filter string for the query
            // Sage 100 uses a specific filter syntax
            string filterParts = "";
            
            if (!string.IsNullOrEmpty(request.Name))
            {
                // Use LIKE for partial name matching
                filterParts += $"CustomerName$ LIKE \"%{EscapeFilter(request.Name)}%\"";
            }
            
            if (!string.IsNullOrEmpty(request.City))
            {
                if (!string.IsNullOrEmpty(filterParts)) filterParts += " AND ";
                filterParts += $"City$ LIKE \"%{EscapeFilter(request.City)}%\"";
            }
            
            if (!string.IsNullOrEmpty(request.State))
            {
                if (!string.IsNullOrEmpty(filterParts)) filterParts += " AND ";
                filterParts += $"State$ = \"{EscapeFilter(request.State)}\"";
            }

            _logger.LogDebug("Filter: {Filter}", filterParts);

            // Try to use nFind with filter, or fall back to iterating
            int foundCount = 0;
            bool hasMore = true;
            
            // Move to first record
            object firstResult = customerSvc.nMoveFirst();
            int moveResult = firstResult != null ? Convert.ToInt32(firstResult) : 0;
            
            if (moveResult == 0)
            {
                _logger.LogInformation("No customers found or error moving to first record");
                return new CustomerSearchResponse
                {
                    Customers = customers,
                    TotalCount = 0,
                    SearchCriteria = BuildSearchCriteria(request)
                };
            }

            // Iterate through customers and filter in code
            // Limit total records scanned to prevent hanging on large databases
            const int maxRecordsToScan = 500;
            int recordsScanned = 0;
            
            while (hasMore && foundCount < request.Limit && recordsScanned < maxRecordsToScan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                recordsScanned++;
                
                // Log progress every 100 records
                if (recordsScanned % 100 == 0)
                {
                    _logger.LogDebug("Scanned {Scanned} records, found {Found} matches so far", 
                        recordsScanned, foundCount);
                }
                
                // Get current record values
                string custName = GetStringValue(customerSvc, "CustomerName$");
                string city = GetStringValue(customerSvc, "City$");
                string state = GetStringValue(customerSvc, "State$");
                string phone = GetStringValue(customerSvc, "TelephoneNo$");
                string address1 = GetStringValue(customerSvc, "AddressLine1$");
                
                // Check if this record matches our criteria
                bool matches = true;
                
                if (!string.IsNullOrEmpty(request.Name))
                {
                    // Use fuzzy name matching - normalize both and check for keyword overlap
                    matches = matches && FuzzyNameMatch(request.Name, custName);
                }
                
                if (!string.IsNullOrEmpty(request.City))
                {
                    matches = matches && (city?.IndexOf(request.City, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                
                if (!string.IsNullOrEmpty(request.State))
                {
                    matches = matches && string.Equals(state, request.State, StringComparison.OrdinalIgnoreCase);
                }
                
                if (!string.IsNullOrEmpty(request.Phone))
                {
                    var normalizedPhone = NormalizePhone(request.Phone);
                    var recordPhone = NormalizePhone(phone);
                    matches = matches && (!string.IsNullOrEmpty(normalizedPhone) && 
                        recordPhone?.Contains(normalizedPhone) == true);
                }
                
                if (!string.IsNullOrEmpty(request.Address))
                {
                    matches = matches && (address1?.IndexOf(request.Address, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (matches)
                {
                    _logger.LogDebug("Found match: {CustomerName}", custName);
                    var customer = ExtractCustomerFromCurrentRecord(customerSvc);
                    customers.Add(customer);
                    foundCount++;
                }

                // Move to next record
                object nextResult = customerSvc.nMoveNext();
                int nextMoveResult = nextResult != null ? Convert.ToInt32(nextResult) : 0;
                hasMore = nextMoveResult == 1;
            }
            
            if (recordsScanned >= maxRecordsToScan && foundCount < request.Limit)
            {
                _logger.LogWarning("Stopped scanning after {MaxRecords} records. Found {Found} matches. " +
                    "Use more specific search criteria for better results.", maxRecordsToScan, foundCount);
            }

            _logger.LogInformation("Found {Count} customers matching criteria", customers.Count);
            
            return new CustomerSearchResponse
            {
                Customers = customers,
                TotalCount = customers.Count,
                SearchCriteria = BuildSearchCriteria(request)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers");
            throw;
        }
        finally
        {
            if (customerSvc != null && Marshal.IsComObject(customerSvc))
            {
                Marshal.ReleaseComObject(customerSvc);
            }
            
            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    public async Task<CustomerDto?> GetCustomerAsync(
        string customerNumber,
        CancellationToken cancellationToken = default)
    {
        // Parse customer number format "01-D3375"
        string divisionNo = "01";
        string customerNo = customerNumber;
        
        if (customerNumber.Length > 3 && customerNumber[2] == '-')
        {
            divisionNo = customerNumber.Substring(0, 2);
            customerNo = customerNumber.Substring(3);
        }

        return await GetCustomerAsync(divisionNo, customerNo, cancellationToken);
    }

    public async Task<CustomerDto?> GetCustomerAsync(
        string arDivisionNo,
        string customerNo,
        CancellationToken cancellationToken = default)
    {
        SessionWrapper? session = null;
        dynamic? customerSvc = null;
        
        try
        {
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            _logger.LogInformation("Getting customer: {Division}-{CustomerNo}", arDivisionNo, customerNo);

            customerSvc = session.ProvideXScript.NewObject("AR_Customer_svc", session.Session);
            
            if (customerSvc == null)
            {
                throw new InvalidOperationException("Failed to create AR_Customer_svc object");
            }

            // Find the specific customer - _svc objects use nFind or iterate
            // First move to first record
            object firstResult = customerSvc.nMoveFirst();
            if (firstResult == null || Convert.ToInt32(firstResult) == 0)
            {
                _logger.LogWarning("No customers in database or error");
                return null;
            }
            
            // Iterate to find the specific customer
            bool found = false;
            bool hasMore = true;
            int scanned = 0;
            
            while (hasMore && scanned < 1000)
            {
                scanned++;
                string recDiv = GetStringValue(customerSvc, "ARDivisionNo$");
                string recCust = GetStringValue(customerSvc, "CustomerNo$");
                
                if (recDiv == arDivisionNo && recCust == customerNo)
                {
                    found = true;
                    break;
                }
                
                object nextResult = customerSvc.nMoveNext();
                hasMore = nextResult != null && Convert.ToInt32(nextResult) == 1;
            }
            
            if (!found)
            {
                _logger.LogWarning("Customer not found after scanning {Scanned} records: {Division}-{CustomerNo}", 
                    scanned, arDivisionNo, customerNo);
                return null;
            }
            
            _logger.LogDebug("Found customer after scanning {Scanned} records", scanned);

            CustomerDto customer = ExtractCustomerFromCurrentRecord(customerSvc);
            
            // Get ship-to addresses for this customer
            List<CustomerShipToDto> shipTos = await GetShipToAddressesAsync(
                session, arDivisionNo, customerNo, cancellationToken);
            
            // Mark the default ship-to based on the customer's DefaultShipToCode
            if (!string.IsNullOrEmpty(customer.DefaultShipToCode))
            {
                foreach (var shipTo in shipTos)
                {
                    if (shipTo.ShipToCode == customer.DefaultShipToCode)
                    {
                        shipTo.IsDefault = true;
                        _logger.LogInformation("Marked ship-to {Code} as default for customer {Customer}",
                            shipTo.ShipToCode, customer.CustomerNumber);
                        break;
                    }
                }
            }
            
            customer.ShipToAddresses = shipTos;
            
            // Find default ship-to
            customer.DefaultShipTo = shipTos.FirstOrDefault(s => s.IsDefault)
                ?? shipTos.FirstOrDefault();

            return customer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {Division}-{CustomerNo}", arDivisionNo, customerNo);
            throw;
        }
        finally
        {
            if (customerSvc != null && Marshal.IsComObject(customerSvc))
            {
                Marshal.ReleaseComObject(customerSvc);
            }
            
            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    public async Task<ValidateShipToResponse> ValidateShipToAsync(
        string customerNumber,
        ValidateShipToRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerNumber, cancellationToken);
        
        if (customer == null)
        {
            return new ValidateShipToResponse
            {
                Matched = false,
                IsDefaultShipTo = false,
                MatchConfidence = 0,
                Differences = new List<string> { "Customer not found" }
            };
        }

        // Find best matching ship-to address
        CustomerShipToDto? bestMatch = null;
        double bestConfidence = 0;
        var differences = new List<string>();

        foreach (var shipTo in customer.ShipToAddresses)
        {
            var (confidence, diffs) = CalculateAddressMatch(request, shipTo);
            
            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestMatch = shipTo;
                differences = diffs;
            }
        }

        // Also check default ship-to specifically
        if (customer.DefaultShipTo != null)
        {
            var (defaultConfidence, defaultDiffs) = CalculateAddressMatch(request, customer.DefaultShipTo);
            
            return new ValidateShipToResponse
            {
                Matched = bestConfidence >= 0.8, // 80% threshold for match
                IsDefaultShipTo = bestMatch?.IsDefault == true,
                MatchedShipToCode = bestMatch?.ShipToCode,
                WarehouseCode = bestMatch?.WarehouseCode,
                ShipVia = bestMatch?.ShipVia,
                MatchConfidence = bestConfidence,
                MatchedAddress = bestMatch,
                Differences = differences
            };
        }

        return new ValidateShipToResponse
        {
            Matched = false,
            IsDefaultShipTo = false,
            MatchConfidence = 0,
            Differences = new List<string> { "No ship-to addresses found for customer" }
        };
    }

    private async Task<List<CustomerShipToDto>> GetShipToAddressesAsync(
        SessionWrapper session,
        string arDivisionNo,
        string customerNo,
        CancellationToken cancellationToken)
    {
        var shipTos = new List<CustomerShipToDto>();
        dynamic? shipToSvc = null;
        
        try
        {
            // Create SO_ShipToAddress_svc to get ship-to addresses
            shipToSvc = session.ProvideXScript.NewObject("SO_ShipToAddress_svc", session.Session);
            
            if (shipToSvc == null)
            {
                _logger.LogWarning("Could not create SO_ShipToAddress_svc");
                return shipTos;
            }

            _logger.LogDebug("Scanning ship-to addresses for {Division}-{CustomerNo}", arDivisionNo, customerNo);
            
            // Move to first record (nSetKeyValue doesn't filter, just sets values)
            object firstResult = shipToSvc.nMoveFirst();
            int moveResult = firstResult != null ? Convert.ToInt32(firstResult) : 0;
            
            if (moveResult != 1)
            {
                _logger.LogDebug("No ship-to addresses in database");
                return shipTos;
            }
            
            bool hasMore = true;
            int scanned = 0;
            int maxScan = 1000; // Scan limit - balance between speed and completeness
            
            while (hasMore && scanned < maxScan)
            {
                scanned++;
                cancellationToken.ThrowIfCancellationRequested();
                
                // Check if this record belongs to our customer
                string recordDiv = GetStringValue(shipToSvc, "ARDivisionNo$");
                string recordCust = GetStringValue(shipToSvc, "CustomerNo$");
                
                // Log first few records to debug matching
                if (scanned <= 5)
                {
                    _logger.LogInformation("Ship-to record {N}: Div=[{Div}] Cust=[{Cust}] (looking for [{LookDiv}]-[{LookCust}])",
                        scanned, recordDiv, recordCust, arDivisionNo, customerNo);
                }
                
                if (recordDiv == arDivisionNo && recordCust == customerNo)
                {
                    string shipToCode = GetStringValue(shipToSvc, "ShipToCode$");
                    
                    var shipTo = new CustomerShipToDto
                    {
                        ShipToCode = shipToCode,
                        Name = GetStringValue(shipToSvc, "ShipToName$"),
                        Address1 = GetStringValue(shipToSvc, "ShipToAddress1$"),
                        Address2 = GetStringValue(shipToSvc, "ShipToAddress2$"),
                        City = GetStringValue(shipToSvc, "ShipToCity$"),
                        State = GetStringValue(shipToSvc, "ShipToState$"),
                        ZipCode = GetStringValue(shipToSvc, "ShipToZipCode$"),
                        Country = GetStringValue(shipToSvc, "ShipToCountryCode$"),
                        WarehouseCode = GetStringValue(shipToSvc, "WarehouseCode$"),
                        ShipVia = GetStringValue(shipToSvc, "ShipVia$"),
                        IsDefault = false // Will be set later based on customer's DefaultShipToCode
                    };
                    
                    shipTos.Add(shipTo);
                    
                    // If we found enough, stop looking
                    if (shipTos.Count >= 30)
                    {
                        break;
                    }
                }

                object nextResult = shipToSvc.nMoveNext();
                int nextMoveResult = nextResult != null ? Convert.ToInt32(nextResult) : 0;
                hasMore = nextMoveResult == 1;
            }

            _logger.LogInformation("Found {Count} ship-to addresses for {Division}-{CustomerNo} after scanning {Scanned} records", 
                shipTos.Count, arDivisionNo, customerNo, scanned);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting ship-to addresses for {Division}-{CustomerNo}", 
                arDivisionNo, customerNo);
        }
        finally
        {
            if (shipToSvc != null && Marshal.IsComObject(shipToSvc))
            {
                Marshal.ReleaseComObject(shipToSvc);
            }
        }

        return shipTos;
    }

    private CustomerDto ExtractCustomerFromCurrentRecord(dynamic customerSvc)
    {
        string divisionNo = GetStringValue(customerSvc, "ARDivisionNo$");
        string customerNo = GetStringValue(customerSvc, "CustomerNo$");
        
        // Get default ship-to code from customer record (this is where Sage stores the "Primary" ship-to)
        string defaultShipToCode = GetStringValue(customerSvc, "ShipToCode$");
        if (string.IsNullOrEmpty(defaultShipToCode))
        {
            // Try alternate field name
            defaultShipToCode = GetStringValue(customerSvc, "DefaultShipToCode$");
        }
        
        _logger.LogInformation("Customer {Div}-{Cust} default ship-to code: [{Code}]", 
            divisionNo, customerNo, defaultShipToCode);
        
        return new CustomerDto
        {
            CustomerNumber = $"{divisionNo}-{customerNo}",
            ARDivisionNo = divisionNo,
            CustomerNo = customerNo,
            CustomerName = GetStringValue(customerSvc, "CustomerName$"),
            Status = GetStringValue(customerSvc, "CustomerStatus$"),
            Address1 = GetStringValue(customerSvc, "AddressLine1$"),
            Address2 = GetStringValue(customerSvc, "AddressLine2$"),
            City = GetStringValue(customerSvc, "City$"),
            State = GetStringValue(customerSvc, "State$"),
            ZipCode = GetStringValue(customerSvc, "ZipCode$"),
            Country = GetStringValue(customerSvc, "CountryCode$"),
            Phone = GetStringValue(customerSvc, "TelephoneNo$"),
            PriceLevel = GetStringValue(customerSvc, "PriceLevel$"),
            TaxSchedule = GetStringValue(customerSvc, "TaxSchedule$"),
            TermsCode = GetStringValue(customerSvc, "TermsCode$"),
            DefaultShipToCode = defaultShipToCode
        };
    }

    private string GetStringValue(dynamic obj, string fieldName)
    {
        try
        {
            string value = "";
            obj.nGetValue(fieldName, ref value);
            return value ?? "";
        }
        catch
        {
            return "";
        }
    }
    
    private bool IsYesValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToUpperInvariant();
        return v == "Y" || v == "1" || v == "TRUE" || v == "YES";
    }

    private string EscapeFilter(string value)
    {
        // Escape special characters in filter strings
        return value.Replace("\"", "\"\"").Replace("'", "''");
    }

    private string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return null;
        // Remove all non-digit characters
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private string BuildSearchCriteria(CustomerSearchRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(request.Name)) parts.Add($"name='{request.Name}'");
        if (!string.IsNullOrEmpty(request.City)) parts.Add($"city='{request.City}'");
        if (!string.IsNullOrEmpty(request.State)) parts.Add($"state='{request.State}'");
        if (!string.IsNullOrEmpty(request.Phone)) parts.Add($"phone='{request.Phone}'");
        if (!string.IsNullOrEmpty(request.Address)) parts.Add($"address='{request.Address}'");
        return string.Join(", ", parts);
    }

    private (double confidence, List<string> differences) CalculateAddressMatch(
        ValidateShipToRequest request, 
        CustomerShipToDto shipTo)
    {
        var differences = new List<string>();
        int matchedFields = 0;
        int totalFields = 0;

        // Compare name (fuzzy)
        if (!string.IsNullOrEmpty(request.Name))
        {
            totalFields++;
            if (FuzzyMatch(request.Name, shipTo.Name))
                matchedFields++;
            else
                differences.Add($"Name mismatch: '{request.Name}' vs '{shipTo.Name}'");
        }

        // Compare address1 (fuzzy)
        if (!string.IsNullOrEmpty(request.Address1))
        {
            totalFields++;
            if (FuzzyMatch(request.Address1, shipTo.Address1))
                matchedFields++;
            else
                differences.Add($"Address mismatch: '{request.Address1}' vs '{shipTo.Address1}'");
        }

        // Compare city (exact, case-insensitive)
        if (!string.IsNullOrEmpty(request.City))
        {
            totalFields++;
            if (string.Equals(request.City, shipTo.City, StringComparison.OrdinalIgnoreCase))
                matchedFields++;
            else
                differences.Add($"City mismatch: '{request.City}' vs '{shipTo.City}'");
        }

        // Compare state (exact, case-insensitive)
        if (!string.IsNullOrEmpty(request.State))
        {
            totalFields++;
            if (string.Equals(request.State, shipTo.State, StringComparison.OrdinalIgnoreCase))
                matchedFields++;
            else
                differences.Add($"State mismatch: '{request.State}' vs '{shipTo.State}'");
        }

        // Compare zip (prefix match - handle zip+4)
        if (!string.IsNullOrEmpty(request.ZipCode))
        {
            totalFields++;
            var reqZip = request.ZipCode.Split('-')[0];
            var shipZip = shipTo.ZipCode?.Split('-')[0] ?? "";
            if (reqZip == shipZip)
                matchedFields++;
            else
                differences.Add($"ZipCode mismatch: '{request.ZipCode}' vs '{shipTo.ZipCode}'");
        }

        double confidence = totalFields > 0 ? (double)matchedFields / totalFields : 0;
        return (confidence, differences);
    }

    private bool FuzzyMatch(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        
        // Normalize both strings
        var normA = NormalizeAddress(a);
        var normB = NormalizeAddress(b);
        
        // Check for exact match after normalization
        if (normA == normB) return true;
        
        // Check if one contains the other
        if (normA.Contains(normB) || normB.Contains(normA)) return true;
        
        return false;
    }

    public async Task<CustomerResolutionResponse> ResolveCustomerAsync(
        CustomerResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving customer: Name={Name}, ShipToCity={City}, ShipToState={State}",
            request.CustomerName, 
            request.ShipToAddress?.City,
            request.ShipToAddress?.State);

        var response = new CustomerResolutionResponse();
        
        try
        {
            // Step 1: Search for customers by name
            var searchRequest = new CustomerSearchRequest
            {
                Name = ExtractCompanyName(request.CustomerName),
                Limit = 20  // Get top 20 candidates
            };
            
            var searchResult = await SearchCustomersAsync(searchRequest, cancellationToken);
            
            if (searchResult.Customers.Count == 0)
            {
                response.Resolved = false;
                response.Recommendation = "REJECTED";
                response.Message = $"No customers found matching name '{request.CustomerName}'";
                return response;
            }
            
            _logger.LogInformation("Found {Count} customer candidates", searchResult.Customers.Count);
            
            // Step 2: Pre-filter by name score - only fetch full details for good name matches
            var candidatesWithNameScore = searchResult.Customers
                .Select(c => new { Customer = c, NameScore = ScoreNameMatch(request.CustomerName, c.CustomerName) })
                .OrderByDescending(c => c.NameScore)
                .ToList();
            
            // Only process top 5 candidates with name score >= 50%
            var topCandidates = candidatesWithNameScore
                .Where(c => c.NameScore >= 0.5)
                .Take(5)
                .ToList();
            
            _logger.LogInformation("Processing {Count} top candidates (filtered from {Total} by name score)",
                topCandidates.Count, searchResult.Customers.Count);
            
            // Step 3: Score each top candidate with full details
            foreach (var item in topCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Get full customer details with ship-to addresses
                var fullCustomer = await GetCustomerAsync(item.Customer.CustomerNumber, cancellationToken);
                if (fullCustomer == null) continue;
                
                var matchResult = ScoreCustomerMatch(request, fullCustomer);
                matchResult.CustomerDetails = fullCustomer;
                response.Candidates.Add(matchResult);
            }
            
            // Step 3: Sort by score and get best match
            response.Candidates = response.Candidates
                .OrderByDescending(c => c.Score)
                .ToList();
            
            if (response.Candidates.Count == 0)
            {
                response.Resolved = false;
                response.Recommendation = "REJECTED";
                response.Message = "Could not score any customer matches";
                return response;
            }
            
            response.BestMatch = response.Candidates.First();
            response.Confidence = response.BestMatch.Score;
            
            // Step 4: Determine recommendation based on confidence
            var issues = new List<string>();
            
            if (response.Confidence >= request.MinConfidence)
            {
                response.Resolved = true;
                response.Recommendation = "AUTO_PROCESS";
                response.Message = $"High confidence match: {response.BestMatch.CustomerName} " +
                    $"(Score: {response.Confidence:P0})";
                
                // Check if this is the default ship-to
                if (!response.BestMatch.IsDefaultShipTo)
                {
                    response.Resolved = false;
                    response.Recommendation = "MANUAL_REVIEW";
                    issues.Add("PO ship-to does NOT match customer's default ship-to address");
                }
            }
            else if (response.Confidence >= 0.5)
            {
                response.Resolved = false;
                response.Recommendation = "MANUAL_REVIEW";
                response.Message = $"Medium confidence match: {response.BestMatch.CustomerName} " +
                    $"(Score: {response.Confidence:P0}). Manual verification recommended.";
            }
            else
            {
                response.Resolved = false;
                response.Recommendation = "REJECTED";
                response.Message = $"Low confidence: Best match is {response.BestMatch.CustomerName} " +
                    $"(Score: {response.Confidence:P0}). Cannot auto-process.";
            }
            
            // Step 5: Validate ship-to has required fields for order processing
            if (response.BestMatch.IsDefaultShipTo || response.Confidence >= 0.5)
            {
                // Check for missing warehouse code
                if (string.IsNullOrWhiteSpace(response.BestMatch.WarehouseCode))
                {
                    response.Resolved = false;
                    response.Recommendation = "MANUAL_REVIEW";
                    issues.Add("Ship-to address has no warehouse code configured in Sage");
                }
                
                // Check for missing ship via
                if (string.IsNullOrWhiteSpace(response.BestMatch.ShipVia))
                {
                    response.Resolved = false;
                    response.Recommendation = "MANUAL_REVIEW";
                    issues.Add("Ship-to address has no ship via method configured in Sage");
                }
            }
            
            // Add issues to message if any
            if (issues.Count > 0)
            {
                response.Message += " - ISSUES: " + string.Join("; ", issues);
            }
            
            // Add scoring details
            response.ScoringDetails = response.BestMatch.ScoreBreakdown.Details;
            
            // Add issue details to scoring
            foreach (var issue in issues)
            {
                response.ScoringDetails.Add($"⚠️ {issue}");
            }
            
            _logger.LogInformation(
                "Customer resolution: {Recommendation} - {CustomerNumber} ({Score:P0})",
                response.Recommendation, 
                response.BestMatch.CustomerNumber, 
                response.Confidence);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving customer");
            throw;
        }
    }
    
    private CustomerMatchResult ScoreCustomerMatch(
        CustomerResolutionRequest request, 
        CustomerDto customer)
    {
        var result = new CustomerMatchResult
        {
            CustomerNumber = customer.CustomerNumber,
            CustomerName = customer.CustomerName
        };
        
        var breakdown = new MatchScoreBreakdown();
        
        // 1. Score name match (weight: 20%)
        breakdown.NameScore = ScoreNameMatch(request.CustomerName, customer.CustomerName);
        breakdown.Details.Add($"Name match: {breakdown.NameScore:P0} " +
            $"('{request.CustomerName}' vs '{customer.CustomerName}')");
        
        // 2. Score ship-to address match (weight: 50% - most important!)
        if (request.ShipToAddress != null && customer.ShipToAddresses.Count > 0)
        {
            var (bestShipTo, shipToScore, isDefault) = FindBestShipToMatch(
                request.ShipToAddress, customer.ShipToAddresses);
            
            breakdown.ShipToScore = shipToScore;
            result.MatchedShipToCode = bestShipTo?.ShipToCode;
            result.IsDefaultShipTo = isDefault;
            result.WarehouseCode = bestShipTo?.WarehouseCode;
            result.ShipVia = bestShipTo?.ShipVia;
            
            breakdown.Details.Add($"Ship-to match: {breakdown.ShipToScore:P0} " +
                $"(matched code: {result.MatchedShipToCode ?? "none"}, isDefault: {isDefault})");
            
            // Bonus for matching default ship-to
            if (isDefault && shipToScore > 0.7)
            {
                breakdown.DefaultShipToBonus = 0.1;
                breakdown.Details.Add($"Default ship-to bonus: +{breakdown.DefaultShipToBonus:P0}");
            }
        }
        else
        {
            breakdown.Details.Add("Ship-to match: N/A (no ship-to data)");
        }
        
        // 3. Score billing address match (weight: 20%)
        if (request.BillingAddress != null)
        {
            breakdown.BillingScore = ScoreAddressMatch(request.BillingAddress, 
                customer.Address1, customer.City, customer.State, customer.ZipCode);
            breakdown.Details.Add($"Billing address match: {breakdown.BillingScore:P0}");
        }
        
        // 4. Score phone match (weight: 10%)
        if (!string.IsNullOrEmpty(request.Phone))
        {
            breakdown.PhoneScore = ScorePhoneMatch(request.Phone, customer.Phone);
            breakdown.Details.Add($"Phone match: {breakdown.PhoneScore:P0}");
        }
        
        // Calculate weighted total score
        // Ship-to is most important (50%), then name (20%), billing (20%), phone (10%)
        result.Score = 
            (breakdown.NameScore * 0.20) +
            (breakdown.ShipToScore * 0.50) +
            (breakdown.BillingScore * 0.20) +
            (breakdown.PhoneScore * 0.10) +
            breakdown.DefaultShipToBonus;
        
        // Cap at 1.0
        result.Score = Math.Min(1.0, result.Score);
        
        result.ScoreBreakdown = breakdown;
        breakdown.Details.Add($"Total weighted score: {result.Score:P0}");
        
        return result;
    }
    
    private double ScoreNameMatch(string requestName, string customerName)
    {
        if (string.IsNullOrEmpty(requestName) || string.IsNullOrEmpty(customerName))
            return 0;
        
        var normRequest = NormalizeName(requestName);
        var normCustomer = NormalizeName(customerName);
        
        // Exact match
        if (normRequest == normCustomer) return 1.0;
        
        // One contains the other
        if (normCustomer.Contains(normRequest) || normRequest.Contains(normCustomer))
            return 0.9;
        
        // Check for significant word overlap
        var requestWords = normRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var customerWords = normCustomer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        int matchedWords = requestWords.Count(rw => 
            customerWords.Any(cw => cw.Contains(rw) || rw.Contains(cw)));
        
        if (requestWords.Length > 0)
            return (double)matchedWords / requestWords.Length * 0.8;
        
        return 0;
    }
    
    private string NormalizeName(string name)
    {
        return name
            .ToUpperInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Replace("INC", "")
            .Replace("LLC", "")
            .Replace("CORP", "")
            .Replace("CORPORATION", "")
            .Replace("COMPANY", "")
            .Replace("CO", "")
            .Replace("(NC)", "")
            .Replace("(SC)", "")
            .Replace("(GA)", "")
            .Replace("(FL)", "")
            .Replace("(VA)", "")
            .Replace("(TN)", "")
            .Replace("  ", " ")
            .Trim();
    }
    
    /// <summary>
    /// Fuzzy match customer names - handles variations like "United Refrigeration, Inc." vs "UNITED REFRIGERATION INC (NC)"
    /// </summary>
    private bool FuzzyNameMatch(string searchName, string? recordName)
    {
        if (string.IsNullOrEmpty(searchName) || string.IsNullOrEmpty(recordName))
            return false;
        
        // Normalize both names
        var normSearch = NormalizeName(searchName);
        var normRecord = NormalizeName(recordName);
        
        // Direct match after normalization
        if (normRecord.Contains(normSearch) || normSearch.Contains(normRecord))
            return true;
        
        // Split into words and check for significant overlap
        var searchWords = normSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)  // Ignore short words
            .ToList();
        
        if (searchWords.Count == 0)
            return false;
        
        // Count how many search words appear in the record
        int matchedWords = searchWords.Count(sw => normRecord.Contains(sw));
        
        // Require at least 50% of significant words to match
        double matchRatio = (double)matchedWords / searchWords.Count;
        return matchRatio >= 0.5;
    }
    
    private (CustomerShipToDto? shipTo, double score, bool isDefault) FindBestShipToMatch(
        AddressInfo requestAddress,
        List<CustomerShipToDto> shipTos)
    {
        CustomerShipToDto? bestMatch = null;
        double bestScore = 0;
        bool isDefault = false;
        
        foreach (var shipTo in shipTos)
        {
            // Pass Address2 for better matching when street address is on different lines
            var score = ScoreAddressMatch(requestAddress, 
                shipTo.Address1, shipTo.City, shipTo.State, shipTo.ZipCode, shipTo.Address2);
            
            // Also check name if provided
            if (!string.IsNullOrEmpty(requestAddress.Name) && !string.IsNullOrEmpty(shipTo.Name))
            {
                var nameScore = ScoreNameMatch(requestAddress.Name, shipTo.Name);
                score = (score + nameScore) / 2;
            }
            
            _logger.LogDebug("Ship-to {Code} score: {Score:P0} (Addr: {Addr1}/{Addr2}, City: {City}, State: {State})",
                shipTo.ShipToCode, score, shipTo.Address1, shipTo.Address2, shipTo.City, shipTo.State);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = shipTo;
                isDefault = shipTo.IsDefault;
            }
        }
        
        return (bestMatch, bestScore, isDefault);
    }
    
    private double ScoreAddressMatch(AddressInfo request, 
        string? address1, string? city, string? state, string? zipCode, string? address2 = null)
    {
        // Combine address lines for comparison
        var reqAddrCombined = CombineAddressLines(request.Address1, request.Address2);
        var sageAddrCombined = CombineAddressLines(address1, address2);
        
        return ScoreAddressMatchCombined(reqAddrCombined, request.City, request.State, request.ZipCode,
            sageAddrCombined, city, state, zipCode);
    }
    
    private string CombineAddressLines(params string?[] lines)
    {
        return string.Join(" ", lines.Where(l => !string.IsNullOrWhiteSpace(l) && l.ToUpper() != "ATTN:"));
    }
    
    private double ScoreAddressMatchCombined(
        string? reqAddr, string? reqCity, string? reqState, string? reqZip,
        string? addr, string? city, string? state, string? zip)
    {
        int matched = 0;
        int total = 0;
        
        // State match (most important for disambiguation)
        if (!string.IsNullOrEmpty(reqState))
        {
            total += 2;  // Weight state higher
            if (string.Equals(reqState, state, StringComparison.OrdinalIgnoreCase))
                matched += 2;
        }
        
        // City match
        if (!string.IsNullOrEmpty(reqCity))
        {
            total++;
            if (string.Equals(reqCity, city, StringComparison.OrdinalIgnoreCase))
                matched++;
        }
        
        // Zip match (first 5 digits)
        if (!string.IsNullOrEmpty(reqZip))
        {
            total++;
            var reqZip5 = reqZip.Split('-')[0];
            var zip5 = zip?.Split('-')[0] ?? "";
            if (reqZip5 == zip5)
                matched++;
        }
        
        // Address match (fuzzy) - compare combined, normalized addresses
        if (!string.IsNullOrEmpty(reqAddr))
        {
            total++;
            var normReq = NormalizeAddress(reqAddr);
            var normAddr = NormalizeAddress(addr ?? "");
            
            // Check if normalized addresses match or one contains the other
            if (normReq == normAddr || normReq.Contains(normAddr) || normAddr.Contains(normReq))
                matched++;
        }
        
        return total > 0 ? (double)matched / total : 0;
    }
    
    private double ScorePhoneMatch(string? requestPhone, string? customerPhone)
    {
        if (string.IsNullOrEmpty(requestPhone) || string.IsNullOrEmpty(customerPhone))
            return 0;
        
        var normRequest = NormalizePhone(requestPhone);
        var normCustomer = NormalizePhone(customerPhone);
        
        if (string.IsNullOrEmpty(normRequest) || string.IsNullOrEmpty(normCustomer))
            return 0;
        
        if (normRequest == normCustomer) return 1.0;
        if (normRequest.Contains(normCustomer) || normCustomer.Contains(normRequest))
            return 0.8;
        
        return 0;
    }
    
    private string ExtractCompanyName(string fullName)
    {
        // Remove common location suffixes for searching
        return fullName
            .Replace("(NC)", "")
            .Replace("(SC)", "")
            .Replace("(GA)", "")
            .Replace("(FL)", "")
            .Trim();
    }

    private string NormalizeAddress(string address)
    {
        return address
            .ToUpperInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Replace("STREET", "ST")
            .Replace("AVENUE", "AVE")
            .Replace("BOULEVARD", "BLVD")
            .Replace("DRIVE", "DR")
            .Replace("ROAD", "RD")
            .Replace("LANE", "LN")
            .Replace("COURT", "CT")
            .Replace("NORTH", "N")
            .Replace("SOUTH", "S")
            .Replace("EAST", "E")
            .Replace("WEST", "W")
            .Replace("  ", " ")
            .Trim();
    }
}

