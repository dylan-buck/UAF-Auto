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
        Console.WriteLine("=== Sage 100 BOI Diagnostic Test ===");
        Console.WriteLine("");

        // Configure these values or set environment variables
        string sage100Path = Environment.GetEnvironmentVariable("SAGE_SERVER_PATH") ?? @"\\uaf-erp\Sage Premium 2022\MAS90\Home";
        string username = Environment.GetEnvironmentVariable("SAGE_USERNAME") ?? "YOUR_USERNAME";
        string password = Environment.GetEnvironmentVariable("SAGE_PASSWORD") ?? "YOUR_PASSWORD";
        string companyCode = Environment.GetEnvironmentVariable("SAGE_COMPANY") ?? "TST";

        object pvx = null;
        object session = null;

        try
        {
            Console.WriteLine("Step 1: Creating ProvideX instance...");
            Type pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            pvx = Activator.CreateInstance(pvxType);
            pvxType.InvokeMember("Init", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { sage100Path });
            Console.WriteLine("SUCCESS: ProvideX initialized");

            Console.WriteLine("Step 2: Creating session...");
            session = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
            Type sessionType = session.GetType();
            sessionType.InvokeMember("nSetUser", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { username, password });
            sessionType.InvokeMember("nSetCompany", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { companyCode });
            Console.WriteLine("SUCCESS: Session created and authenticated");

            Console.WriteLine("");
            Console.WriteLine("Step 3: Testing different object creation methods...");
            Console.WriteLine("");

            // Try GetObject instead of NewObject
            Console.WriteLine("Method A: Trying GetObject(\"SO_SalesOrder_bus\", session)...");
            try
            {
                object obj = pvxType.InvokeMember("GetObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus", session });
                Console.WriteLine("SUCCESS: GetObject worked!");
                if (obj != null)
                {
                    Console.WriteLine("Object type: " + obj.GetType().ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + GetInnerExceptionMessage(ex));
            }

            Console.WriteLine("");

            // Try NewObject with just the name
            Console.WriteLine("Method B: Trying NewObject(\"SO_SalesOrder_bus\")...");
            try
            {
                object obj = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus" });
                Console.WriteLine("SUCCESS: NewObject without session worked!");
                if (obj != null)
                {
                    Console.WriteLine("Object type: " + obj.GetType().ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + GetInnerExceptionMessage(ex));
            }

            Console.WriteLine("");

            // Try with session
            Console.WriteLine("Method C: Trying NewObject(\"SO_SalesOrder_bus\", session)...");
            try
            {
                object obj = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus", session });
                Console.WriteLine("SUCCESS: NewObject with session worked!");
                if (obj != null)
                {
                    Console.WriteLine("Object type: " + obj.GetType().ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + GetInnerExceptionMessage(ex));
            }

            Console.WriteLine("");

            // Try checking if we can call methods on session to verify it's working
            Console.WriteLine("Step 4: Testing session methods...");
            try
            {
                object userResult = sessionType.InvokeMember("nGetValue", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { "UserName$" });
                Console.WriteLine("SUCCESS: Can call nGetValue on session. UserName: " + (userResult != null ? userResult.ToString() : "null"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("WARNING: Cannot call nGetValue: " + GetInnerExceptionMessage(ex));
            }

            Console.WriteLine("");
            Console.WriteLine("Step 5: Checking if SO module is accessible...");
            try
            {
                // Try to create a simpler object first
                object testObj = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
                Console.WriteLine("SUCCESS: Can create SY_Session object (test)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED: " + GetInnerExceptionMessage(ex));
            }

            Console.WriteLine("");
            Console.WriteLine("========================================");
            Console.WriteLine("Diagnostic complete. Review results above.");
            Console.WriteLine("========================================");

        }
        catch (Exception ex)
        {
            Console.WriteLine("");
            Console.WriteLine("ERROR: " + GetInnerExceptionMessage(ex));
            Console.WriteLine("Full: " + ex.Message);
        }
        finally
        {
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

