using System.Runtime.InteropServices;
using SageBOI.Api.Models;

namespace SageBOI.Api.Services;

public class SalesOrderService : ISalesOrderService
{
    private readonly IProvideXSessionManager _sessionManager;
    private readonly ILogger<SalesOrderService> _logger;

    public SalesOrderService(
        IProvideXSessionManager sessionManager,
        ILogger<SalesOrderService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<BOIResult> CreateSalesOrderAsync(SalesOrderDTO order)
    {
        SessionWrapper? session = null;
        
        try
        {
            _logger.LogInformation("Creating sales order for customer {Customer}, PO {PONumber}", 
                order.CustomerNumber, order.PONumber);

            // Get a session from the pool
            session = await _sessionManager.GetSessionAsync();
            
            // Set Program Context (Best Practice for Security/Auditing)
            try {
                dynamic sess = session.Session;
                int taskId = sess.nLookupTask("SO_SalesOrder_ui");
                sess.nSetProgram(taskId);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to set program context to SO_SalesOrder_ui");
            }

            // Create the SO_SalesOrder_bus object
            // Use NewObject with session to ensure proper context
            dynamic salesOrder = session.ProvideXScript.NewObject("SO_SalesOrder_bus", session.Session);
            
            // 1. Get Next Sales Order Number
            string nextOrderNo = "";
            try 
            {
                // We use reflection here because nGetNextSalesOrderNo uses a ByRef string parameter
                // which is tricky with dynamic COM objects
                object[] args = new object[] { "" };
                ParameterModifier[] modifiers = new ParameterModifier[1];
                modifiers[0] = new ParameterModifier(1);
                modifiers[0][0] = true; // First arg is ByRef

                salesOrder.GetType().InvokeMember(
                    "nGetNextSalesOrderNo",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    salesOrder,
                    args,
                    modifiers,
                    null,
                    null
                );
                nextOrderNo = args[0].ToString();
                _logger.LogInformation("Generated Next Sales Order Number: {OrderNo}", nextOrderNo);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get next sales order number: {ex.Message}", ex);
            }

            // 2. Set Key (Start the order)
            int setKeyRet = salesOrder.nSetKey(nextOrderNo);
            if (setKeyRet == 0) // 0 is failure
            {
                string error = salesOrder.sLastErrorMsg;
                throw new InvalidOperationException($"Failed to set key '{nextOrderNo}': {error}");
            }

            // Set header information
            salesOrder.nSetKeyValue("ARDivisionNo$", "00"); // Default division
            salesOrder.nSetKeyValue("CustomerNo$", order.CustomerNumber);
            salesOrder.nSetValue("CustomerPONo$", order.PONumber);
            
            if (!string.IsNullOrEmpty(order.OrderDate))
            {
                salesOrder.nSetValue("OrderDate$", order.OrderDate);
            }
            
            if (!string.IsNullOrEmpty(order.ShipDate))
            {
                salesOrder.nSetValue("ShipExpireDate$", order.ShipDate);
            }
            
            if (!string.IsNullOrEmpty(order.Comment))
            {
                salesOrder.nSetValue("Comment$", order.Comment);
            }

            // Set ship-to address if provided
            if (order.ShipToAddress != null)
            {
                SetShipToAddress(salesOrder, order.ShipToAddress);
            }

            // Add line items
            int lineKey = 1;
            foreach (var line in order.Lines)
            {
                _logger.LogDebug("Adding line item {LineKey}: {ItemCode} x {Quantity}", 
                    lineKey, line.ItemCode, line.Quantity);

                // Create new line
                int addLineRet = salesOrder.nAddLine();
                if (addLineRet == 0)
                {
                     _logger.LogWarning("nAddLine failed: {Error}", (string)salesOrder.sLastErrorMsg);
                     // Try explicit SetKey if AddLine fails? Usually AddLine is enough.
                }

                salesOrder.nSetValue("ItemCode$", line.ItemCode);
                salesOrder.nSetValue("QuantityOrdered", line.Quantity);
                
                if (line.UnitPrice.HasValue)
                {
                    salesOrder.nSetValue("UnitPrice", line.UnitPrice.Value);
                }
                
                if (!string.IsNullOrEmpty(line.Description))
                {
                    salesOrder.nSetValue("ItemCodeDesc$", line.Description);
                }
                
                if (!string.IsNullOrEmpty(line.WarehouseCode))
                {
                    salesOrder.nSetValue("WarehouseCode$", line.WarehouseCode);
                }

                // Write the line
                int writeResult = salesOrder.nWrite(); // Write the line object
                if (writeResult != 1) // 1 is success
                {
                    string error = salesOrder.sLastErrorMsg;
                    _logger.LogError("Failed to write line {LineKey}. Result: {Result}, Error: {Error}", lineKey, writeResult, error);
                    return new BOIResult
                    {
                        Success = false,
                        ErrorCode = $"LINE_WRITE_{writeResult}",
                        ErrorMessage = $"Failed to write line item {lineKey}: {error}"
                    };
                }

                lineKey++;
            }

            // Write the order
            _logger.LogDebug("Writing sales order to Sage 100");
            int orderWriteResult = salesOrder.nWrite();
            
            if (orderWriteResult == 1) // 1 is success
            {
                _logger.LogInformation("Successfully created sales order {SalesOrderNo} for customer {Customer}", 
                    nextOrderNo, order.CustomerNumber);
                
                return new BOIResult
                {
                    Success = true,
                    SalesOrderNumber = nextOrderNo
                };
            }
            else
            {
                string error = salesOrder.sLastErrorMsg;
                _logger.LogError("Failed to write sales order. Result: {Result}, Error: {Error}", orderWriteResult, error);
                return new BOIResult
                {
                    Success = false,
                    ErrorCode = $"ORDER_WRITE_{orderWriteResult}",
                    ErrorMessage = $"Failed to create sales order: {error}"
                };
            }
        }
        catch (COMException comEx)
        {
            _logger.LogError(comEx, "COM error creating sales order. HRESULT: {HResult}", comEx.HResult);
            return new BOIResult
            {
                Success = false,
                ErrorCode = $"COM_{comEx.HResult:X}",
                ErrorMessage = $"COM error: {comEx.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating sales order");
            return new BOIResult
            {
                Success = false,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
        finally
        {
            // Always return the session to the pool
            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    private void SetShipToAddress(dynamic salesOrder, AddressDTO address)
    {
        if (!string.IsNullOrEmpty(address.Name))
        {
            salesOrder.nSetValue("ShipToName$", address.Name);
        }
        
        if (!string.IsNullOrEmpty(address.Address1))
        {
            salesOrder.nSetValue("ShipToAddress1$", address.Address1);
        }
        
        if (!string.IsNullOrEmpty(address.Address2))
        {
            salesOrder.nSetValue("ShipToAddress2$", address.Address2);
        }
        
        if (!string.IsNullOrEmpty(address.City))
        {
            salesOrder.nSetValue("ShipToCity$", address.City);
        }
        
        if (!string.IsNullOrEmpty(address.State))
        {
            salesOrder.nSetValue("ShipToState$", address.State);
        }
        
        if (!string.IsNullOrEmpty(address.ZipCode))
        {
            salesOrder.nSetValue("ShipToZipCode$", address.ZipCode);
        }
        
        if (!string.IsNullOrEmpty(address.Country))
        {
            salesOrder.nSetValue("ShipToCountryCode$", address.Country);
        }
    }
}

