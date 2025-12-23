using System.Reflection;
using System.Runtime.InteropServices;
using UAFMiddleware.Models;

namespace UAFMiddleware.Services;

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

    public async Task<SalesOrderResponse> CreateSalesOrderAsync(
        SalesOrderRequest request, 
        CancellationToken cancellationToken = default)
    {
        SessionWrapper? session = null;
        dynamic? salesOrder = null;
        
        try
        {
            _logger.LogInformation(
                "Creating sales order for customer {Customer}, PO {PONumber}, Lines: {LineCount}", 
                request.CustomerNumber, request.PONumber, request.Lines.Count);

            // Get a session from the pool
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            _logger.LogDebug("Got session {SessionId}", session.SessionId);

            // Try to set program context (best practice for auditing)
            try
            {
                dynamic sess = session.Session;
                int taskId = sess.nLookupTask("SO_SalesOrder_ui");
                if (taskId != 0)
                {
                    sess.nSetProgram(taskId);
                    _logger.LogDebug("Set program context to SO_SalesOrder_ui");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not set program context (non-fatal)");
            }

            // Create the SO_SalesOrder_bus object
            _logger.LogDebug("Creating SO_SalesOrder_bus object");
            salesOrder = session.ProvideXScript.NewObject("SO_SalesOrder_bus", session.Session);
            
            if (salesOrder == null)
            {
                throw new InvalidOperationException("Failed to create SO_SalesOrder_bus object");
            }

            // Get next sales order number
            string nextOrderNo = GetNextSalesOrderNumber(salesOrder);
            _logger.LogInformation("Generated sales order number: {OrderNo}", nextOrderNo);

            // Set the key to start the order
            int setKeyRet = salesOrder.nSetKey(nextOrderNo);
            if (setKeyRet == 0)
            {
                string error = salesOrder.sLastErrorMsg ?? "Unknown error";
                throw new InvalidOperationException($"Failed to set key '{nextOrderNo}': {error}");
            }

            // Set header information
            string divisionNo = request.ARDivisionNo ?? "00";
            salesOrder.nSetValue("ARDivisionNo$", divisionNo);
            salesOrder.nSetValue("CustomerNo$", request.CustomerNumber);
            salesOrder.nSetValue("CustomerPONo$", request.PONumber);
            
            if (!string.IsNullOrEmpty(request.OrderDate))
            {
                salesOrder.nSetValue("OrderDate$", request.OrderDate);
            }
            else
            {
                salesOrder.nSetValue("OrderDate$", DateTime.Now.ToString("yyyyMMdd"));
            }
            
            if (!string.IsNullOrEmpty(request.ShipDate))
            {
                salesOrder.nSetValue("ShipExpireDate$", request.ShipDate);
            }
            
            if (!string.IsNullOrEmpty(request.Comment))
            {
                salesOrder.nSetValue("Comment$", request.Comment);
            }

            // Set ship-to address if provided
            if (request.ShipToAddress != null)
            {
                SetShipToAddress(salesOrder, request.ShipToAddress);
            }

            // Add line items
            int lineNum = 0;
            foreach (var line in request.Lines)
            {
                lineNum++;
                _logger.LogDebug("Adding line {LineNum}: {ItemCode} x {Quantity}", 
                    lineNum, line.ItemCode, line.Quantity);

                // Add new line
                int addLineRet = salesOrder.nAddLine();
                if (addLineRet == 0)
                {
                    string lineError = salesOrder.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("nAddLine warning: {Error}", lineError);
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
                int lineWriteResult = salesOrder.nWrite();
                if (lineWriteResult != 1)
                {
                    string lineWriteError = salesOrder.sLastErrorMsg ?? "Unknown error";
                    _logger.LogError("Failed to write line {LineNum}: {Error}", lineNum, lineWriteError);
                    return new SalesOrderResponse
                    {
                        Success = false,
                        ErrorCode = $"LINE_WRITE_ERROR",
                        ErrorMessage = $"Failed to write line item {lineNum} ({line.ItemCode}): {lineWriteError}"
                    };
                }
            }

            // Write the order (final commit)
            _logger.LogDebug("Writing sales order to Sage 100");
            int orderWriteResult = salesOrder.nWrite();
            
            if (orderWriteResult == 1)
            {
                _logger.LogInformation(
                    "Successfully created sales order {SalesOrderNo} for customer {Customer}", 
                    nextOrderNo, request.CustomerNumber);
                
                return new SalesOrderResponse
                {
                    Success = true,
                    SalesOrderNumber = nextOrderNo,
                    Message = $"Sales order {nextOrderNo} created successfully"
                };
            }
            else
            {
                string orderError = salesOrder.sLastErrorMsg ?? "Unknown error";
                _logger.LogError("Failed to write sales order: {Error}", orderError);
                return new SalesOrderResponse
                {
                    Success = false,
                    ErrorCode = "ORDER_WRITE_ERROR",
                    ErrorMessage = $"Failed to create sales order: {orderError}"
                };
            }
        }
        catch (COMException comEx)
        {
            _logger.LogError(comEx, "COM error creating sales order. HRESULT: 0x{HResult:X}", comEx.HResult);
            return new SalesOrderResponse
            {
                Success = false,
                ErrorCode = $"COM_ERROR",
                ErrorMessage = $"Sage 100 COM error: {comEx.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating sales order");
            return new SalesOrderResponse
            {
                Success = false,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
        finally
        {
            // Release COM objects
            if (salesOrder != null)
            {
                try
                {
                    if (Marshal.IsComObject(salesOrder))
                    {
                        Marshal.ReleaseComObject(salesOrder);
                    }
                }
                catch { /* ignore cleanup errors */ }
            }

            // Always return the session to the pool
            if (session != null)
            {
                _sessionManager.ReleaseSession(session);
            }
        }
    }

    private string GetNextSalesOrderNumber(dynamic salesOrder)
    {
        try
        {
            // Use reflection for ByRef parameter handling
            object[] args = new object[] { "" };
            ParameterModifier[] modifiers = new ParameterModifier[1];
            modifiers[0] = new ParameterModifier(1);
            modifiers[0][0] = true; // First arg is ByRef

            salesOrder.GetType().InvokeMember(
                "nGetNextSalesOrderNo",
                BindingFlags.InvokeMethod,
                null,
                salesOrder,
                args,
                modifiers,
                null,
                null
            );
            
            return args[0]?.ToString() ?? throw new InvalidOperationException("nGetNextSalesOrderNo returned null");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get next sales order number: {ex.Message}", ex);
        }
    }

    private void SetShipToAddress(dynamic salesOrder, ShipToAddress address)
    {
        if (!string.IsNullOrEmpty(address.Name))
            salesOrder.nSetValue("ShipToName$", address.Name);
        
        if (!string.IsNullOrEmpty(address.Address1))
            salesOrder.nSetValue("ShipToAddress1$", address.Address1);
        
        if (!string.IsNullOrEmpty(address.Address2))
            salesOrder.nSetValue("ShipToAddress2$", address.Address2);
        
        if (!string.IsNullOrEmpty(address.City))
            salesOrder.nSetValue("ShipToCity$", address.City);
        
        if (!string.IsNullOrEmpty(address.State))
            salesOrder.nSetValue("ShipToState$", address.State);
        
        if (!string.IsNullOrEmpty(address.ZipCode))
            salesOrder.nSetValue("ShipToZipCode$", address.ZipCode);
        
        if (!string.IsNullOrEmpty(address.Country))
            salesOrder.nSetValue("ShipToCountryCode$", address.Country);
    }
}

