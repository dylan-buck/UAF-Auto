using System.Reflection;
using System.Runtime.InteropServices;

Console.WriteLine("===========================================");
Console.WriteLine("Sage 100 BOI Sales Order Test");
Console.WriteLine("===========================================");
Console.WriteLine();

// CONFIGURE THESE VALUES
string serverPath = @"\\uaf-erp\Sage Premium 2022\MAS90\Home";
string company = "TST";
string customerDivision = "01";
string customerNo = "A0075";
string itemCode = "14202";
decimal quantity = 1;

Console.Write("Enter Sage Username: ");
string username = Console.ReadLine() ?? "";
Console.Write("Enter Sage Password: ");
string password = Console.ReadLine() ?? "";

dynamic? pvx = null;
dynamic? session = null;
dynamic? salesOrder = null;

try
{
    // Step 1: Create ProvideX.Script
    Console.WriteLine();
    Console.WriteLine("[1] Creating ProvideX.Script COM object...");
    Type? pvxType = Type.GetTypeFromProgID("ProvideX.Script");
    if (pvxType == null)
    {
        Console.WriteLine("FAILED: ProvideX.Script not registered");
        return;
    }
    pvx = Activator.CreateInstance(pvxType);
    Console.WriteLine("    SUCCESS: ProvideX.Script created");

    // Step 2: Initialize
    Console.WriteLine($"[2] Initializing with path: {serverPath}");
    pvx.Init(serverPath);
    Console.WriteLine("    SUCCESS: Initialized");

    // Step 3: Create session
    Console.WriteLine("[3] Creating SY_Session...");
    session = pvx.NewObject("SY_Session");
    if (session == null)
    {
        Console.WriteLine("FAILED: NewObject returned null");
        return;
    }
    Console.WriteLine("    SUCCESS: SY_Session created");

    // Step 4: Authenticate
    Console.WriteLine($"[4] Authenticating user: {username}");
    int userRet = session.nSetUser(username, password);
    Console.WriteLine($"    nSetUser returned: {userRet}");
    if (userRet == 0)
    {
        Console.WriteLine($"    FAILED: {session.sLastErrorMsg}");
        return;
    }
    Console.WriteLine("    SUCCESS: User authenticated");

    // Step 5: Set company
    Console.WriteLine($"[5] Setting company: {company}");
    int companyRet = session.nSetCompany(company);
    Console.WriteLine($"    nSetCompany returned: {companyRet}");
    if (companyRet == 0)
    {
        Console.WriteLine($"    FAILED: {session.sLastErrorMsg}");
        return;
    }
    Console.WriteLine("    SUCCESS: Company set");

    // Step 6: Set module
    Console.WriteLine("[6] Setting module to S/O...");
    try
    {
        session.nSetModule("S/O");
        session.nSetDate("S/O", DateTime.Now.ToString("yyyyMMdd"));
        Console.WriteLine("    SUCCESS: Module set");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    WARNING: {ex.Message} (non-fatal)");
    }

    // Step 6b: Set program context (might be required before creating business objects)
    Console.WriteLine("[6b] Setting program context...");
    try
    {
        int taskId = session.nLookupTask("SO_SalesOrder_ui");
        Console.WriteLine($"    nLookupTask('SO_SalesOrder_ui') returned: {taskId}");
        if (taskId != 0)
        {
            int progRet = session.nSetProgram(taskId);
            Console.WriteLine($"    nSetProgram returned: {progRet}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    WARNING: {ex.Message} (non-fatal)");
    }

    // Step 7: Create SO_SalesOrder_bus - try different approaches
    Console.WriteLine("[7] Creating SO_SalesOrder_bus...");
    
    // First, list available Sales Order objects
    Console.WriteLine("    Trying to discover available SO objects...");
    string[] objectsToTry = new[] {
        "SO_SalesOrder_bus",
        "SO_SalesOrder_Bus", 
        "SO_SalesOrder_BUS",
        "SalesOrder_bus",
        "SO_Order_bus"
    };
    
    foreach (var objName in objectsToTry)
    {
        Console.WriteLine($"    Trying: {objName}...");
        try
        {
            salesOrder = pvx.NewObject(objName, session);
            if (salesOrder != null)
            {
                Console.WriteLine($"    SUCCESS with: {objName}");
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Failed: {ex.Message}");
            salesOrder = null;
        }
    }
    
    if (salesOrder == null)
    {
        Console.WriteLine("FAILED: Could not create any Sales Order object");
        Console.WriteLine();
        Console.WriteLine("This might indicate:");
        Console.WriteLine("  - S/O module not licensed for this company");
        Console.WriteLine("  - User doesn't have Sales Order permissions");
        Console.WriteLine("  - Business object name is different in this Sage version");
        return;
    }

    // Step 8: Get next order number
    Console.WriteLine("[8] Getting next sales order number...");
    string nextOrderNo = "";
    object[] getNumArgs = new object[] { "" };
    ParameterModifier[] mods = new ParameterModifier[1];
    mods[0] = new ParameterModifier(1);
    mods[0][0] = true;
    
    salesOrder.GetType().InvokeMember(
        "nGetNextSalesOrderNo",
        BindingFlags.InvokeMethod,
        null,
        salesOrder,
        getNumArgs,
        mods,
        null,
        null
    );
    nextOrderNo = getNumArgs[0]?.ToString() ?? "";
    Console.WriteLine($"    Next order number: {nextOrderNo}");

    // Step 9: Set key
    Console.WriteLine($"[9] Calling nSetKey({nextOrderNo})...");
    int setKeyRet = salesOrder.nSetKey(nextOrderNo);
    Console.WriteLine($"    nSetKey returned: {setKeyRet}");
    if (setKeyRet == 0)
    {
        Console.WriteLine($"    FAILED: {salesOrder.sLastErrorMsg}");
        return;
    }

    // Step 10: Set header fields
    Console.WriteLine($"[10] Setting header fields...");
    
    int divRet = salesOrder.nSetValue("ARDivisionNo$", customerDivision);
    Console.WriteLine($"    ARDivisionNo$ = {customerDivision}, result: {divRet}");
    
    int custRet = salesOrder.nSetValue("CustomerNo$", customerNo);
    Console.WriteLine($"    CustomerNo$ = {customerNo}, result: {custRet}");
    if (custRet == 0)
    {
        Console.WriteLine($"    WARNING: {salesOrder.sLastErrorMsg}");
    }

    salesOrder.nSetValue("CustomerPONo$", "TEST-ORDER-001");
    salesOrder.nSetValue("OrderDate$", DateTime.Now.ToString("yyyyMMdd"));

    // Step 11: Get lines object via InvokeMember
    Console.WriteLine("[11] Getting oLines object via InvokeMember...");
    Type soType = ((object)salesOrder).GetType();
    object linesObj = soType.InvokeMember(
        "oLines",
        BindingFlags.GetProperty,
        null,
        salesOrder,
        null
    ) ?? throw new Exception("oLines returned null");
    Console.WriteLine($"    Got oLines, type: {linesObj.GetType().Name}");
    
    // Cast to dynamic for easier use
    dynamic lines = linesObj;

    // Step 11b: Check what columns are available
    Console.WriteLine("[11b] Checking available columns on lines object...");
    try
    {
        string columns = lines.sColumns;
        Console.WriteLine($"    Columns ({columns?.Length ?? 0} chars):");
        // Print in chunks
        if (!string.IsNullOrEmpty(columns))
        {
            var colList = columns.Split(',');
            for (int i = 0; i < colList.Length; i += 10)
            {
                var chunk = string.Join(", ", colList.Skip(i).Take(10));
                Console.WriteLine($"      {chunk}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Could not get columns: {ex.Message}");
    }

    // Step 12: Add line via InvokeMember
    Console.WriteLine("[12] Adding line with nAddLine() via InvokeMember...");
    Type linesType2 = linesObj.GetType();
    object? addLineRetObj = linesType2.InvokeMember(
        "nAddLine",
        BindingFlags.InvokeMethod,
        null,
        linesObj,
        null
    );
    Console.WriteLine($"    nAddLine returned: {addLineRetObj}");
    
    // Step 12b: Explore the lines object
    Console.WriteLine("[12b] Exploring lines object methods and properties...");
    try
    {
        // Try to get current line number
        Console.WriteLine("    Trying lines.nLineCount...");
        try { Console.WriteLine($"      nLineCount: {lines.nLineCount}"); } catch (Exception ex) { Console.WriteLine($"      Error: {ex.Message}"); }
        
        Console.WriteLine("    Trying lines.nLineNo...");
        try { Console.WriteLine($"      nLineNo: {lines.nLineNo}"); } catch (Exception ex) { Console.WriteLine($"      Error: {ex.Message}"); }
        
        // Try different ways to set item code
        Console.WriteLine("    Trying lines.sValue('ItemCode$')...");
        try 
        { 
            string currentItem = lines.sValue("ItemCode$");
            Console.WriteLine($"      Current ItemCode$: '{currentItem}'"); 
        } 
        catch (Exception ex) { Console.WriteLine($"      Error: {ex.Message}"); }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Exploration failed: {ex.Message}");
    }

    // Step 13: Try using Type.InvokeMember for proper late binding
    Console.WriteLine($"[13] Setting ItemCode$ = '{itemCode}' using InvokeMember...");
    Console.WriteLine($"    Lines object type: {linesType2.Name}");
    
    // Try nSetValue via InvokeMember
    Console.WriteLine("    [13a] Calling nSetValue via InvokeMember...");
    try
    {
        object? result = linesType2.InvokeMember(
            "nSetValue",
            BindingFlags.InvokeMethod,
            null,
            linesObj,
            new object[] { "ItemCode$", itemCode }
        );
        Console.WriteLine($"      Result: {result} (type: {result?.GetType().Name ?? "null"})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      Error: {ex.Message}");
    }
    
    // Try to read the value back using GetValue
    Console.WriteLine("    [13b] Reading ItemCode$ via InvokeMember GetValue...");
    try
    {
        string readValue = "";
        object[] getValArgs = new object[] { "ItemCode$", readValue };
        ParameterModifier[] getMods = new ParameterModifier[1];
        getMods[0] = new ParameterModifier(2);
        getMods[0][1] = true; // Second arg is ByRef
        
        object? result = linesType2.InvokeMember(
            "nGetValue",
            BindingFlags.InvokeMethod,
            null,
            linesObj,
            getValArgs,
            getMods,
            null,
            null
        );
        Console.WriteLine($"      nGetValue result: {result}, ItemCode$ = '{getValArgs[1]}'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      Error: {ex.Message}");
    }
    
    // Try listing methods on the object
    Console.WriteLine("    [13c] Exploring COM object methods...");
    try
    {
        // Get IDispatch type info if possible
        var typeLib = System.Runtime.InteropServices.Marshal.GetIDispatchForObject(linesObj);
        Console.WriteLine($"      Got IDispatch pointer");
        System.Runtime.InteropServices.Marshal.Release(typeLib);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      Error: {ex.Message}");
    }

    // Step 14: Set quantity via InvokeMember
    Console.WriteLine($"[14] Setting QuantityOrdered = {quantity} via InvokeMember...");
    try
    {
        object? qtyRetObj = linesType2.InvokeMember(
            "nSetValue",
            BindingFlags.InvokeMethod,
            null,
            linesObj,
            new object[] { "QuantityOrdered", (double)quantity }
        );
        Console.WriteLine($"    Result: {qtyRetObj} (type: {qtyRetObj?.GetType().Name ?? "null"})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Exception: {ex.Message}");
    }

    // Step 15: Write line via InvokeMember
    Console.WriteLine("[15] Writing line with oLines.nWrite() via InvokeMember...");
    try
    {
        object? lineWriteRetObj = linesType2.InvokeMember(
            "nWrite",
            BindingFlags.InvokeMethod,
            null,
            linesObj,
            null
        );
        Console.WriteLine($"    Result: {lineWriteRetObj} (type: {lineWriteRetObj?.GetType().Name ?? "null"})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Exception: {ex.Message}");
    }

    // Step 16: Write order
    Console.WriteLine("[16] Writing order with salesOrder.nWrite()...");
    object? orderWriteRetObj = salesOrder.nWrite();
    Console.WriteLine($"    Raw result: {orderWriteRetObj} (type: {orderWriteRetObj?.GetType().Name ?? "null"})");
    int orderWriteRet = orderWriteRetObj != null ? Convert.ToInt32(orderWriteRetObj) : -1;
    Console.WriteLine($"    Converted result: {orderWriteRet}");
    if (orderWriteRet == 1)
    {
        Console.WriteLine();
        Console.WriteLine($"*** SUCCESS! Order {nextOrderNo} created! ***");
    }
    else
    {
        string orderError = "";
        try { orderError = salesOrder.sLastErrorMsg ?? ""; } catch { }
        Console.WriteLine($"    FAILED: '{orderError}'");
    }

    Console.WriteLine();
    Console.WriteLine("===========================================");
    Console.WriteLine("Test Complete");
    Console.WriteLine("===========================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"EXCEPTION: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}
finally
{
    // Cleanup
    if (salesOrder != null && Marshal.IsComObject(salesOrder))
        Marshal.ReleaseComObject(salesOrder);
    if (session != null && Marshal.IsComObject(session))
        Marshal.ReleaseComObject(session);
    if (pvx != null && Marshal.IsComObject(pvx))
        Marshal.ReleaseComObject(pvx);
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

