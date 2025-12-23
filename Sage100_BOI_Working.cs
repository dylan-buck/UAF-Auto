using System;
using System.Runtime.InteropServices;

class Program
{
    static string GetInnerExceptionMessage(Exception ex)
    {
        Exception current = ex;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current.Message;
    }

    static void Main()
    {
        Console.WriteLine("=== Sage 100 BOI Test ===");
        Console.WriteLine("");

        // Path to Sage 100 Home directory (UNC from server)
        string sage100Path = @"\\uaf-erp\Sage Premium 2022\MAS90\Home";

        // Configure these values or set environment variables
        string username = Environment.GetEnvironmentVariable("SAGE_USERNAME") ?? "YOUR_USERNAME";
        string password = Environment.GetEnvironmentVariable("SAGE_PASSWORD") ?? "YOUR_PASSWORD";
        string companyCode = Environment.GetEnvironmentVariable("SAGE_COMPANY") ?? "TST";

        object pvx = null;
        object session = null;
        object salesOrder = null;

        try
        {
            Console.WriteLine("Step 1: Getting ProvideX.Script COM object...");
            Type pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            if (pvxType == null)
            {
                Console.WriteLine("ERROR: ProvideX.Script object not registered.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("SUCCESS: ProvideX.Script found");

            Console.WriteLine("Step 2: Creating ProvideX instance...");
            pvx = Activator.CreateInstance(pvxType);
            Console.WriteLine("SUCCESS: ProvideX instance created");

            Console.WriteLine("Step 3: Initializing ProvideX with server path...");
            Console.WriteLine("Path: " + sage100Path);
            pvxType.InvokeMember("Init", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { sage100Path });
            Console.WriteLine("SUCCESS: ProvideX initialized");

            Console.WriteLine("Step 4: Creating SY_Session object...");
            session = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
            if (session == null)
            {
                 Console.WriteLine("ERROR: Failed to create SY_Session object.");
                 return;
            }
            Console.WriteLine("SUCCESS: Session object created");

            Type sessionType = session.GetType();

            // Helper to get last error
            Func<string> getLastError = () => {
                try {
                    return (string)sessionType.InvokeMember("sLastErrorMsg", System.Reflection.BindingFlags.GetProperty, null, session, null);
                } catch { return "Could not retrieve error message"; }
            };

            Console.WriteLine("Step 5: Setting user credentials...");
            Console.WriteLine("Username: " + username);
            object userRet = sessionType.InvokeMember("nSetUser", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { username, password });
            
            if (userRet == null)
            {
                Console.WriteLine("WARNING: nSetUser returned null.");
            }
            else if (userRet.ToString() == "0")
            {
                Console.WriteLine("ERROR: Failed to set user. Error: " + getLastError());
                return;
            }
            Console.WriteLine("SUCCESS: User credentials set");

            Console.WriteLine("Step 6: Setting company...");
            Console.WriteLine("Company Code: " + companyCode);
            object companyRet = sessionType.InvokeMember("nSetCompany", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { companyCode });
            
            if (companyRet == null)
            {
                 Console.WriteLine("ERROR: nSetCompany returned null (unexpected).");
                 Console.WriteLine("LastError: " + getLastError());
                 return;
            }
            
            Console.WriteLine("nSetCompany returned type: " + companyRet.GetType().ToString());
            Console.WriteLine("nSetCompany returned value: " + companyRet.ToString());

            if (companyRet.ToString() == "0")
            {
                Console.WriteLine("ERROR: Failed to set company. Error: " + getLastError());
                return;
            }
            Console.WriteLine("SUCCESS: Company set");

            Console.WriteLine("Step 7: Setting module context (S/O)...");
            object moduleRet = sessionType.InvokeMember("nSetModule", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { "S/O" });
            if (moduleRet.ToString() == "0")
            {
                Console.WriteLine("WARNING: Could not set module context. Error: " + getLastError());
            }
            else
            {
                Console.WriteLine("SUCCESS: Module context set");
            }

            Console.WriteLine("Step 8: Setting date context...");
            string today = System.DateTime.Now.ToString("yyyyMMdd");
            object dateRet = sessionType.InvokeMember("nSetDate", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { "S/O", today });
            if (dateRet.ToString() == "0")
            {
                Console.WriteLine("WARNING: Could not set date context. Error: " + getLastError());
            }
            else
            {
                Console.WriteLine("SUCCESS: Date context set to " + today);
            }

            Console.WriteLine("Step 9: Trying to set program context...");
            string[] taskNames = { "SO_SalesOrder_ui", "SO_SalesOrderEntry", "SO_SalesOrder", "SO_SalesOrderMaintenance" };
            bool programSet = false;
            
            foreach (string taskName in taskNames)
            {
                Console.WriteLine("  Trying task name: " + taskName);
                object taskId = sessionType.InvokeMember("nLookupTask", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { taskName });
                if (Convert.ToInt32(taskId) != 0)
                {
                    Console.WriteLine("  SUCCESS: Task ID found: " + taskId.ToString());
                    object progRet = sessionType.InvokeMember("nSetProgram", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { taskId });
                    if (progRet.ToString() != "0")
                    {
                        Console.WriteLine("  SUCCESS: Program context set with " + taskName);
                        programSet = true;
                        break;
                    }
                }
                Console.WriteLine("  Failed to lookup/set task. Error: " + getLastError());
            }
            
            if (!programSet)
            {
                Console.WriteLine("WARNING: Could not set program context with any task name");
            }

            Console.WriteLine("Step 11: Attempting to create SO_SalesOrder_bus object...");
            
            // Method 1: Using session.GetObject() (per Sage documentation)
            Console.WriteLine("Trying method 1: session.GetObject(\"SO_SalesOrder_bus\")...");
            try
            {
                object obj = sessionType.InvokeMember("GetObject", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { "SO_SalesOrder_bus" });
                if (obj != null)
                {
                    Console.WriteLine("SUCCESS: GetObject returned an object!");
                    salesOrder = obj;
                }
                else
                {
                     Console.WriteLine("Method 1 failed: GetObject returned null. Error: " + getLastError());
                }
            }
            catch (Exception ex1)
            {
                Console.WriteLine("Method 1 failed: " + GetInnerExceptionMessage(ex1) + " LastError: " + getLastError());
                
                // Method 2: NewObject with session parameter (original method)
                Console.WriteLine("Trying method 2: pvx.NewObject(\"SO_SalesOrder_bus\", session)...");
                try
                {
                    salesOrder = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus", session });
                    Console.WriteLine("SUCCESS: NewObject with session worked!");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Method 2 failed: " + GetInnerExceptionMessage(ex2) + " LastError: " + getLastError());
                    
                    // Method 3: NewObject without session parameter
                    Console.WriteLine("Trying method 3: pvx.NewObject(\"SO_SalesOrder_bus\")...");
                    try
                    {
                        salesOrder = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus" });
                        Console.WriteLine("SUCCESS: NewObject without session worked!");
                    }
                    catch (Exception ex3)
                    {
                        Console.WriteLine("Method 3 failed: " + GetInnerExceptionMessage(ex3) + " LastError: " + getLastError());
                    }
                }
            }
            
            if (salesOrder == null)
            {
                Console.WriteLine("ERROR: Failed to create SO_SalesOrder_bus object.");
            }
            else
            {
                Console.WriteLine("SUCCESS: SO_SalesOrder_bus object created!");
                Console.WriteLine("");
                Console.WriteLine("========================================");
                Console.WriteLine("ALL TESTS PASSED!");
                Console.WriteLine("========================================");
                Console.WriteLine("The workstation is fully compatible for");
                Console.WriteLine("Sage 100 BOI integration middleware.");
                Console.WriteLine("========================================");
            }
        }
        catch (COMException comEx)
        {
            Console.WriteLine("");
            Console.WriteLine("COM ERROR: " + comEx.Message);
            Console.WriteLine("HRESULT: 0x" + comEx.HResult.ToString("X"));
            if (comEx.InnerException != null)
            {
                Console.WriteLine("Inner Exception: " + comEx.InnerException.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("");
            Console.WriteLine("ERROR: " + GetInnerExceptionMessage(ex));
            Console.WriteLine("Full Exception: " + ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
            }
            Console.WriteLine("");
            Console.WriteLine("Stack Trace:");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // Cleanup COM objects
            if (salesOrder != null && Marshal.IsComObject(salesOrder))
                Marshal.ReleaseComObject(salesOrder);
            if (session != null && Marshal.IsComObject(session))
                Marshal.ReleaseComObject(session);
            if (pvx != null && Marshal.IsComObject(pvx))
                Marshal.ReleaseComObject(pvx);
        }

        Console.WriteLine("");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
