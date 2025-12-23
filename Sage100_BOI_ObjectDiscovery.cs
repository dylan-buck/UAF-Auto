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
        Console.WriteLine("=== Sage 100 BOI Object Discovery ===");
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
            Type pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            pvx = Activator.CreateInstance(pvxType);
            pvxType.InvokeMember("Init", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { sage100Path });
            session = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
            Type sessionType = session.GetType();
            sessionType.InvokeMember("nSetUser", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { username, password });
            sessionType.InvokeMember("nSetCompany", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { companyCode });

            Console.WriteLine("Session created and authenticated.");
            Console.WriteLine("");

            // Try different object name variations
            string[] objectNames = {
                "SO_SalesOrder_bus",
                "SO_SalesOrder_BUS",
                "SO_SalesOrderBus",
                "SO570_SalesOrder_BUS",
                "SO570_SalesOrder_bus",
                "SO_SalesOrder",
                "SalesOrder_bus",
                "SO_SalesOrder_bus.pvc"
            };

            Console.WriteLine("Testing different object name variations:");
            Console.WriteLine("");

            foreach (string objName in objectNames)
            {
                Console.Write("Trying: " + objName + "... ");
                try
                {
                    object obj = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { objName, session });
                    Console.WriteLine("SUCCESS!");
                    if (obj != null)
                    {
                        Console.WriteLine("  Object created: " + obj.GetType().ToString());
                        Marshal.ReleaseComObject(obj);
                    }
                    break; // Found one that works!
                }
                catch (Exception ex)
                {
                    string error = GetInnerExceptionMessage(ex);
                    if (error.Contains("Error: 90"))
                    {
                        Console.WriteLine("Error 90 (same as before)");
                    }
                    else
                    {
                        Console.WriteLine(error);
                    }
                }
            }

            Console.WriteLine("");
            Console.WriteLine("Trying to check if we can list available objects...");
            try
            {
                // Try to see if there's a method to list objects
                System.Reflection.MethodInfo[] methods = pvxType.GetMethods();
                Console.WriteLine("Available methods on ProvideX.Script:");
                foreach (System.Reflection.MethodInfo method in methods)
                {
                    if (method.Name.Contains("Object") || method.Name.Contains("List") || method.Name.Contains("Get"))
                    {
                        Console.WriteLine("  - " + method.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not list methods: " + ex.Message);
            }

            Console.WriteLine("");
            Console.WriteLine("========================================");
            Console.WriteLine("Discovery complete.");
            Console.WriteLine("========================================");

        }
        catch (Exception ex)
        {
            Console.WriteLine("");
            Console.WriteLine("ERROR: " + GetInnerExceptionMessage(ex));
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

