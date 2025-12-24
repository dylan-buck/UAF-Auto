using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

public interface ICustomerService
{
    /// <summary>
    /// Search for customers by name, address, phone, etc.
    /// </summary>
    Task<CustomerSearchResponse> SearchCustomersAsync(
        CustomerSearchRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific customer by customer number (format: "01-D3375")
    /// </summary>
    Task<CustomerDto?> GetCustomerAsync(
        string customerNumber, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific customer by division and customer number
    /// </summary>
    Task<CustomerDto?> GetCustomerAsync(
        string arDivisionNo, 
        string customerNo, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate if a ship-to address matches a customer's default ship-to
    /// </summary>
    Task<ValidateShipToResponse> ValidateShipToAsync(
        string customerNumber, 
        ValidateShipToRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolve/identify the correct customer from PO data using intelligent matching
    /// </summary>
    Task<CustomerResolutionResponse> ResolveCustomerAsync(
        CustomerResolutionRequest request,
        CancellationToken cancellationToken = default);
}

