// Standalone test script - compile and run directly on workstation
// dotnet run TestSalesOrder.cs
// Or: csc TestSalesOrder.cs && TestSalesOrder.exe

using System;
using System.Reflection;
using System.Runtime.InteropServices;

class TestSalesOrder
{
    static void Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Sage 100 BOI Sales Order Test");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // CONFIGURE THESE VALUES
        string serverPath = @"\\uaf-erp\Sage\Sage 100 Premium\MAS90\Home";
        string username = "YOUR_USERNAME";  // Replace with actual
        string password = "YOUR_PASSWORD";  // Replace with actual
        string company = "TST";
        string customerDivision = "01";
        string customerNo = "A0075";
        string itemCode = "14202";
        decimal quantity = 1;
        string warehouseCode = "000";

        if (args.Length >= 2)
        {
            username = args[0];
            password = args[1];
        }
        else
        {
            Console.Write("Enter Sage Username: ");
            username = Console.ReadLine() ?? "";
            Console.Write("Enter Sage Password: ");
            password = Console.ReadLine() ?? "";
        }

        dynamic? pvx = null;
        dynamic? session = null;
        dynamic? salesOrder = null;

        try
        {
            // Step 1: Create ProvideX.Script
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

            // Step 7: Create SO_SalesOrder_bus
            Console.WriteLine("[7] Creating SO_SalesOrder_bus...");
            salesOrder = pvx.NewObject("SO_SalesOrder_bus", session);
            if (salesOrder == null)
            {
                Console.WriteLine("FAILED: NewObject returned null");
                return;
            }
            Console.WriteLine("    SUCCESS: SO_SalesOrder_bus created");

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

            // Step 11: Get lines object
            Console.WriteLine("[11] Getting oLines object...");
            dynamic lines = salesOrder.oLines;
            Console.WriteLine("    SUCCESS: Got oLines");

            // Step 12: Add line
            Console.WriteLine("[12] Adding line with nAddLine()...");
            int addLineRet = lines.nAddLine();
            Console.WriteLine($"    nAddLine returned: {addLineRet}");
            if (addLineRet == 0)
            {
                Console.WriteLine($"    WARNING: {lines.sLastErrorMsg}");
            }

            // Step 13: Try setting ItemCode in different ways
            Console.WriteLine($"[13] Setting ItemCode$ = {itemCode}...");
            
            // First, let's see what columns are available
            Console.WriteLine("    Checking available columns on lines object...");
            try
            {
                string columns = lines.sColumns;
                Console.WriteLine($"    Available columns: {columns?.Substring(0, Math.Min(200, columns?.Length ?? 0))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Could not get columns: {ex.Message}");
            }

            // Try nSetValue
            Console.WriteLine($"    Trying lines.nSetValue('ItemCode$', '{itemCode}')...");
            int itemRet = lines.nSetValue("ItemCode$", itemCode);
            Console.WriteLine($"    Result: {itemRet}");
            if (itemRet == 0)
            {
                string itemError = "";
                try { itemError = lines.sLastErrorMsg ?? ""; } catch { }
                Console.WriteLine($"    Error: '{itemError}'");
                
                // Try alternative approaches
                Console.WriteLine("    Trying alternative: nSetKeyValue...");
                try
                {
                    int keyRet = lines.nSetKeyValue("ItemCode$", itemCode);
                    Console.WriteLine($"    nSetKeyValue result: {keyRet}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    nSetKeyValue failed: {ex.Message}");
                }
            }

            // Step 14: Set quantity
            Console.WriteLine($"[14] Setting QuantityOrdered = {quantity}...");
            try
            {
                int qtyRet = lines.nSetValue("QuantityOrdered", (double)quantity);
                Console.WriteLine($"    Result: {qtyRet}");
                if (qtyRet == 0)
                {
                    Console.WriteLine($"    Error: {lines.sLastErrorMsg}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Exception: {ex.Message}");
            }

            // Step 15: Write line
            Console.WriteLine("[15] Writing line with oLines.nWrite()...");
            int lineWriteRet = lines.nWrite();
            Console.WriteLine($"    nWrite returned: {lineWriteRet}");
            if (lineWriteRet != 1)
            {
                Console.WriteLine($"    Warning/Error: {lines.sLastErrorMsg}");
            }

            // Step 16: Write order
            Console.WriteLine("[16] Writing order with salesOrder.nWrite()...");
            int orderWriteRet = salesOrder.nWrite();
            Console.WriteLine($"    nWrite returned: {orderWriteRet}");
            if (orderWriteRet == 1)
            {
                Console.WriteLine($"    SUCCESS! Order {nextOrderNo} created!");
            }
            else
            {
                Console.WriteLine($"    FAILED: {salesOrder.sLastErrorMsg}");
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
            Console.WriteLine($"Stack: {ex.StackTrace}");
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
    }
}

