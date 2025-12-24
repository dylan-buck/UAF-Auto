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
            while (hasMore && foundCount < request.Limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
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
                    matches = matches && (custName?.IndexOf(request.Name, StringComparison.OrdinalIgnoreCase) >= 0);
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
                    var customer = ExtractCustomerFromCurrentRecord(customerSvc);
                    customers.Add(customer);
                    foundCount++;
                }

                // Move to next record
                object nextResult = customerSvc.nMoveNext();
                int nextMoveResult = nextResult != null ? Convert.ToInt32(nextResult) : 0;
                hasMore = nextMoveResult == 1;
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

            // Set key values to find specific customer
            customerSvc.nSetKeyValue("ARDivisionNo$", arDivisionNo);
            customerSvc.nSetKeyValue("CustomerNo$", customerNo);
            
            object findResult = customerSvc.nSetKey();
            int found = findResult != null ? Convert.ToInt32(findResult) : 0;
            
            if (found != 1)
            {
                _logger.LogWarning("Customer not found: {Division}-{CustomerNo}", arDivisionNo, customerNo);
                return null;
            }

            CustomerDto customer = ExtractCustomerFromCurrentRecord(customerSvc);
            
            // Get ship-to addresses for this customer
            List<CustomerShipToDto> shipTos = await GetShipToAddressesAsync(
                session, arDivisionNo, customerNo, cancellationToken);
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

            // Set filter for this customer
            shipToSvc.nSetKeyValue("ARDivisionNo$", arDivisionNo);
            shipToSvc.nSetKeyValue("CustomerNo$", customerNo);
            
            // Move to first record for this customer
            object firstResult = shipToSvc.nMoveFirst();
            int moveResult = firstResult != null ? Convert.ToInt32(firstResult) : 0;
            
            bool hasMore = moveResult == 1;
            int count = 0;
            
            while (hasMore && count < 100) // Limit to 100 ship-to addresses
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Check if this record belongs to our customer
                string recordDiv = GetStringValue(shipToSvc, "ARDivisionNo$");
                string recordCust = GetStringValue(shipToSvc, "CustomerNo$");
                
                if (recordDiv != arDivisionNo || recordCust != customerNo)
                {
                    break; // Moved past our customer's records
                }

                var shipTo = new CustomerShipToDto
                {
                    ShipToCode = GetStringValue(shipToSvc, "ShipToCode$"),
                    Name = GetStringValue(shipToSvc, "ShipToName$"),
                    Address1 = GetStringValue(shipToSvc, "ShipToAddress1$"),
                    Address2 = GetStringValue(shipToSvc, "ShipToAddress2$"),
                    City = GetStringValue(shipToSvc, "ShipToCity$"),
                    State = GetStringValue(shipToSvc, "ShipToState$"),
                    ZipCode = GetStringValue(shipToSvc, "ShipToZipCode$"),
                    Country = GetStringValue(shipToSvc, "ShipToCountryCode$"),
                    WarehouseCode = GetStringValue(shipToSvc, "WarehouseCode$"),
                    ShipVia = GetStringValue(shipToSvc, "ShipVia$"),
                    IsDefault = GetStringValue(shipToSvc, "DefaultShipTo$") == "Y"
                };
                
                shipTos.Add(shipTo);
                count++;

                object nextResult = shipToSvc.nMoveNext();
                int nextMoveResult = nextResult != null ? Convert.ToInt32(nextResult) : 0;
                hasMore = nextMoveResult == 1;
            }

            _logger.LogDebug("Found {Count} ship-to addresses for {Division}-{CustomerNo}", 
                shipTos.Count, arDivisionNo, customerNo);
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
            TermsCode = GetStringValue(customerSvc, "TermsCode$")
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

