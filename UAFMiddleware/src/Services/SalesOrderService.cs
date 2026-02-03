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
        bool sessionCorrupted = false;
        
        try
        {
            _logger.LogInformation(
                "Creating sales order for customer {Customer}, PO {PONumber}, Lines: {LineCount}", 
                request.CustomerNumber, request.PONumber, request.Lines.Count);

            // Get a session from the pool
            session = await _sessionManager.GetSessionAsync(cancellationToken);
            _logger.LogInformation("Got session {SessionId} (created: {Created}, lastUsed: {LastUsed})", 
                session.SessionId, session.CreatedAt, session.LastUsed);

            // Verify session is still valid
            try
            {
                dynamic sess = session.Session;
                string companyCode = sess.sCompanyCode;
                _logger.LogInformation("Session company code: {CompanyCode}", companyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session appears to be corrupted - will invalidate");
                sessionCorrupted = true;
                throw new InvalidOperationException("Session is no longer valid");
            }

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
            _logger.LogInformation("Creating SO_SalesOrder_bus object...");
            salesOrder = session.ProvideXScript.NewObject("SO_SalesOrder_bus", session.Session);
            
            if (salesOrder == null)
            {
                sessionCorrupted = true;
                throw new InvalidOperationException("Failed to create SO_SalesOrder_bus object");
            }
            _logger.LogInformation("SO_SalesOrder_bus object created successfully");

            // Get next sales order number
            _logger.LogInformation("Getting next sales order number...");
            string nextOrderNo = GetNextSalesOrderNumber(salesOrder);
            _logger.LogInformation("Generated sales order number: {OrderNo}", nextOrderNo);

            // Set the key to initialize the new order using nSetKeyValue + nSetKey() pattern (matches working VBScript)
            _logger.LogInformation("Setting key using nSetKeyValue + nSetKey() pattern...");
            object setKeyValueResultObj = salesOrder.nSetKeyValue("SalesOrderNo$", nextOrderNo);
            int setKeyValueResult = setKeyValueResultObj != null ? Convert.ToInt32(setKeyValueResultObj) : 0;
            _logger.LogInformation("nSetKeyValue('SalesOrderNo$', '{OrderNo}') returned: {Result}", nextOrderNo, setKeyValueResult);
            
            object setKeyResultObj = salesOrder.nSetKey();
            int setKeyResult = setKeyResultObj != null ? Convert.ToInt32(setKeyResultObj) : 0;
            _logger.LogInformation("nSetKey() returned: {Result}", setKeyResult);
            
            if (setKeyResult == 0)
            {
                string setKeyError = salesOrder.sLastErrorMsg ?? "Unknown error";
                _logger.LogError("Failed to set key for order {OrderNo}: {Error}", nextOrderNo, setKeyError);
                throw new InvalidOperationException($"Failed to initialize sales order {nextOrderNo}: {setKeyError}");
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
            
            // Always set automation comment for audit trail
            salesOrder.nSetValue("Comment$", "Created By Automation");

            // Set ship-to code from customer resolution (preferred method)
            // This links the order to an existing ship-to address in Sage
            if (!string.IsNullOrEmpty(request.ShipToCode))
            {
                object shipToResult = salesOrder.nSetValue("ShipToCode$", request.ShipToCode);
                int shipToSetResult = shipToResult != null ? Convert.ToInt32(shipToResult) : 0;
                _logger.LogInformation("Set ShipToCode$ = {ShipToCode}, result: {Result}",
                    request.ShipToCode, shipToSetResult);

                if (shipToSetResult == 0)
                {
                    string shipToError = salesOrder.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("ShipToCode$ set warning: {Error}", shipToError);
                }
            }

            // Set warehouse code from customer resolution
            if (!string.IsNullOrEmpty(request.WarehouseCode))
            {
                object whseResult = salesOrder.nSetValue("WarehouseCode$", request.WarehouseCode);
                int whseSetResult = whseResult != null ? Convert.ToInt32(whseResult) : 0;
                _logger.LogInformation("Set WarehouseCode$ = {WarehouseCode}, result: {Result}",
                    request.WarehouseCode, whseSetResult);
            }

            // Set ship via from customer resolution
            if (!string.IsNullOrEmpty(request.ShipVia))
            {
                object shipViaResult = salesOrder.nSetValue("ShipVia$", request.ShipVia);
                int shipViaSetResult = shipViaResult != null ? Convert.ToInt32(shipViaResult) : 0;
                _logger.LogInformation("Set ShipVia$ = {ShipVia}, result: {Result}",
                    request.ShipVia, shipViaSetResult);
            }

            // Set ship-to address manually only if ShipToCode is not provided
            if (string.IsNullOrEmpty(request.ShipToCode) && request.ShipToAddress != null)
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
                object addLineResultObj = lines.nAddLine();
                int addLineRet = addLineResultObj != null ? Convert.ToInt32(addLineResultObj) : 0;
                _logger.LogInformation("oLines.nAddLine returned: {Result}", addLineRet);
                
                if (addLineRet == 0)
                {
                    string lineError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("nAddLine warning: {Error}", lineError);
                }

                // Transform item code (strip ZLP/ZLPSP prefixes)
                string transformedItemCode = TransformItemCode(line.ItemCode);
                if (transformedItemCode != line.ItemCode)
                {
                    _logger.LogInformation("Transformed ItemCode: '{Original}' → '{Transformed}'",
                        line.ItemCode, transformedItemCode);
                }

                // IMPORTANT: Set ItemCode$ FIRST - this loads item defaults (pricing, warehouse, etc.)
                _logger.LogInformation("Setting ItemCode$ = '{ItemCode}'...", transformedItemCode);
                object? itemResultObj;
                try
                {
                    itemResultObj = lines.nSetValue("ItemCode$", transformedItemCode);
                }
                catch (COMException comEx)
                {
                    _logger.LogError(comEx, "COM exception setting ItemCode$");
                    throw;
                }

                // Handle empty/null returns - treat as failure
                int itemResult = 0;
                if (itemResultObj != null && !string.IsNullOrEmpty(itemResultObj.ToString()))
                {
                    itemResult = Convert.ToInt32(itemResultObj);
                }
                _logger.LogInformation("Set ItemCode$ = {ItemCode}, result: {Result}", transformedItemCode, itemResult);

                if (itemResult == 0)
                {
                    string itemError = "";
                    try { itemError = lines.sLastErrorMsg ?? ""; } catch { }
                    _logger.LogError("ItemCode$ set FAILED. Error: '{Error}'", itemError);

                    // Try to get more info about why it failed
                    try
                    {
                        // Check if item exists via CI_Item lookup
                        dynamic itemObj = session.ProvideXScript.NewObject("CI_Item_bus", session.Session);
                        object findResult = itemObj.nFind(transformedItemCode);
                        int found = findResult != null ? Convert.ToInt32(findResult) : 0;
                        _logger.LogInformation("CI_Item lookup for '{ItemCode}': found={Found}", transformedItemCode, found);
                        if (found == 0)
                        {
                            string findError = itemObj.sLastErrorMsg ?? "";
                            _logger.LogError("Item '{ItemCode}' not found in CI_Item. Error: {Error}", transformedItemCode, findError);
                        }
                        if (Marshal.IsComObject(itemObj)) Marshal.ReleaseComObject(itemObj);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not verify item existence");
                    }

                    // Return structured error response instead of throwing
                    return new SalesOrderResponse
                    {
                        Success = false,
                        ErrorCode = "INVALID_ITEM",
                        ErrorMessage = $"Item '{transformedItemCode}' not found in Sage inventory",
                        InvalidItems = new List<string> { line.ItemCode },
                        Message = $"Line {lineNum}: Invalid item code '{line.ItemCode}'"
                    };
                }

                // Set quantity (convert to double for COM compatibility)
                _logger.LogInformation("Setting QuantityOrdered = {Qty}...", line.Quantity);
                object qtyResultObj;
                try
                {
                    qtyResultObj = lines.nSetValue("QuantityOrdered", Convert.ToDouble(line.Quantity));
                }
                catch (COMException comEx)
                {
                    _logger.LogError(comEx, "COM exception setting QuantityOrdered");
                    throw;
                }
                int qtyResult = qtyResultObj != null ? Convert.ToInt32(qtyResultObj) : 0;
                _logger.LogInformation("Set QuantityOrdered = {Qty}, result: {Result}", line.Quantity, qtyResult);
                if (qtyResult == 0)
                {
                    string qtyError = "";
                    try { qtyError = lines.sLastErrorMsg ?? ""; } catch { }
                    _logger.LogWarning("QuantityOrdered set warning: {Error}", qtyError);
                }

                // Override warehouse if specified (after item code sets the default)
                if (!string.IsNullOrEmpty(line.WarehouseCode))
                {
                    object whseResultObj = lines.nSetValue("WarehouseCode$", line.WarehouseCode);
                    int whseResult = whseResultObj != null ? Convert.ToInt32(whseResultObj) : 0;
                    _logger.LogInformation("Set WarehouseCode$ = {Warehouse}, result: {Result}", line.WarehouseCode, whseResult);
                    if (whseResult == 0)
                    {
                        string whseError = lines.sLastErrorMsg ?? "Unknown error";
                        _logger.LogWarning("WarehouseCode$ set warning: {Error}", whseError);
                    }
                }
                
                // Override unit price if specified
                if (line.UnitPrice.HasValue)
                {
                    object priceResultObj = lines.nSetValue("UnitPrice", Convert.ToDouble(line.UnitPrice.Value));
                    int priceResult = priceResultObj != null ? Convert.ToInt32(priceResultObj) : 0;
                    _logger.LogInformation("Set UnitPrice = {Price}, result: {Result}", line.UnitPrice.Value, priceResult);
                }
                
                // Override description if specified
                if (!string.IsNullOrEmpty(line.Description))
                {
                    lines.nSetValue("ItemCodeDesc$", line.Description);
                }

                // Write the line
                object lineWriteResultObj = lines.nWrite();
                int lineWriteResult = lineWriteResultObj != null ? Convert.ToInt32(lineWriteResultObj) : 0;
                _logger.LogInformation("oLines.nWrite returned: {Result}", lineWriteResult);
                if (lineWriteResult == 0)
                {
                    string lineWriteError = lines.sLastErrorMsg ?? "Unknown error";
                    _logger.LogWarning("Line write warning: {Error}", lineWriteError);
                }

                _logger.LogInformation("Line {LineNum} setup complete", lineNum);
            }

            // Write the order (final commit)
            _logger.LogInformation("Writing sales order to Sage 100...");
            object orderWriteResultObj = salesOrder.nWrite();
            int orderWriteResult = orderWriteResultObj != null ? Convert.ToInt32(orderWriteResultObj) : 0;
            _logger.LogInformation("salesOrder.nWrite() returned: {Result}", orderWriteResult);
            
            if (orderWriteResult == 1)
            {
                // Get the final order number (in case it wasn't retrieved earlier)
                if (string.IsNullOrEmpty(nextOrderNo))
                {
                    try
                    {
                        object orderNoObj = salesOrder.sValue("SalesOrderNo$");
                        nextOrderNo = orderNoObj?.ToString() ?? "UNKNOWN";
                    }
                    catch
                    {
                        nextOrderNo = "CREATED";
                    }
                }
                
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
            sessionCorrupted = true;
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

            // If session is corrupted, invalidate it instead of returning to pool
            if (session != null)
            {
                if (sessionCorrupted)
                {
                    _logger.LogWarning("Invalidating corrupted session {SessionId}", session.SessionId);
                    _sessionManager.InvalidateSession(session);
                }
                else
                {
                    _sessionManager.ReleaseSession(session);
                }
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

            _logger.LogDebug("Calling nGetNextSalesOrderNo via reflection...");
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
            
            string result = args[0]?.ToString() ?? "";
            _logger.LogDebug("nGetNextSalesOrderNo returned: {OrderNo}", result);
            
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException("nGetNextSalesOrderNo returned empty string");
            }
            
            return result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is COMException comEx)
        {
            string errorMsg = salesOrder.sLastErrorMsg ?? comEx.Message;
            _logger.LogError(comEx, "COM error in nGetNextSalesOrderNo: {Error}", errorMsg);
            throw new InvalidOperationException($"Failed to get next sales order number: {errorMsg}", comEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetNextSalesOrderNumber");
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

    private string TransformItemCode(string itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return itemCode;

        var trimmed = itemCode.Trim();

        // ZLPSP prefix (check first - longer prefix)
        if (trimmed.StartsWith("ZLPSP", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(5); // Remove "ZLPSP"
        }

        // ZLP prefix
        if (trimmed.StartsWith("ZLP", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(3); // Remove "ZLP"
        }

        return trimmed;
    }
}

