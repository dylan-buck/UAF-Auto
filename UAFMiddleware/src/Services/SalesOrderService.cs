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
            // Parse customer number - might be in format "00-CUSTNO" or just "CUSTNO"
            // Sage 100 customer numbers are max 7 characters, so if we see "XX-XXXXXX" format, split it
            string divisionNo = "00";
            string customerNo = request.CustomerNumber;
            
            // Check if customer number is in "XX-XXXXXX" format (division-customerno)
            if (customerNo.Length > 3 && customerNo[2] == '-')
            {
                divisionNo = customerNo.Substring(0, 2);
                customerNo = customerNo.Substring(3);
                _logger.LogInformation("Parsed customer: Division={Division}, CustomerNo={CustomerNo}", divisionNo, customerNo);
            }
            else if (!string.IsNullOrEmpty(request.ARDivisionNo))
            {
                divisionNo = request.ARDivisionNo;
            }
            
            int divResult = salesOrder.nSetValue("ARDivisionNo$", divisionNo);
            _logger.LogInformation("Set ARDivisionNo$ = {Division}, result: {Result}", divisionNo, divResult);
            
            int custResult = salesOrder.nSetValue("CustomerNo$", customerNo);
            _logger.LogInformation("Set CustomerNo$ = {CustomerNo}, result: {Result}", customerNo, custResult);
            if (custResult == 0)
            {
                string custError = salesOrder.sLastErrorMsg ?? "Unknown error";
                _logger.LogWarning("CustomerNo$ set warning: {Error}", custError);
            }
            
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

            // Get the lines object - this is where we set line item values
            dynamic lines = salesOrder.oLines;
            _logger.LogInformation("Got oLines object");

            // Add line items
            int lineNum = 0;
            foreach (var line in request.Lines)
            {
                lineNum++;
                _logger.LogInformation("Adding line {LineNum}: {ItemCode} x {Quantity}", 
                    lineNum, line.ItemCode, line.Quantity);

                // Add new line to the lines object
                int addLineRet = lines.nAddLine();
                _logger.LogInformation("oLines.nAddLine returned: {Result}", addLineRet);
                
                if (addLineRet == 0)
                {
                    string lineError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("nAddLine warning: {Error}", lineError);
                }

                // Set warehouse first (required before item code)
                string warehouseCode = line.WarehouseCode ?? "000";
                object whseResultObj = lines.nSetValue("WarehouseCode$", warehouseCode);
                int whseResult = whseResultObj != null ? Convert.ToInt32(whseResultObj) : 0;
                _logger.LogInformation("Set WarehouseCode$ = {Warehouse}, result: {Result}", warehouseCode, whseResult);
                if (whseResult == 0)
                {
                    string whseError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("WarehouseCode$ set warning: {Error}", whseError);
                }
                
                // Set item code on the lines object
                object itemResultObj = lines.nSetValue("ItemCode$", line.ItemCode);
                int itemResult = itemResultObj != null ? Convert.ToInt32(itemResultObj) : 0;
                _logger.LogInformation("Set ItemCode$ = {ItemCode}, result: {Result}", line.ItemCode, itemResult);
                if (itemResult == 0)
                {
                    string itemError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("ItemCode$ set warning: {Error}", itemError);
                }
                
                // Set quantity on the lines object (convert to double for COM compatibility)
                object qtyResultObj = lines.nSetValue("QuantityOrdered", Convert.ToDouble(line.Quantity));
                int qtyResult = qtyResultObj != null ? Convert.ToInt32(qtyResultObj) : 0;
                _logger.LogInformation("Set QuantityOrdered = {Qty}, result: {Result}", line.Quantity, qtyResult);
                if (qtyResult == 0)
                {
                    string qtyError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("QuantityOrdered set warning: {Error}", qtyError);
                }
                
                if (line.UnitPrice.HasValue)
                {
                    object priceResultObj = lines.nSetValue("UnitPrice", Convert.ToDouble(line.UnitPrice.Value));
                    int priceResult = priceResultObj != null ? Convert.ToInt32(priceResultObj) : 0;
                    _logger.LogInformation("Set UnitPrice = {Price}, result: {Result}", line.UnitPrice.Value, priceResult);
                }
                
                if (!string.IsNullOrEmpty(line.Description))
                {
                    lines.nSetValue("ItemCodeDesc$", line.Description);
                }

                // Write the line
                int lineWriteResult = lines.nWrite();
                _logger.LogInformation("oLines.nWrite returned: {Result}", lineWriteResult);
                if (lineWriteResult == 0)
                {
                    string lineWriteError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("Line write warning: {Error}", lineWriteError);
                }

                _logger.LogInformation("Line {LineNum} setup complete", lineNum);
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

