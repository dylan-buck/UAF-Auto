using System;
using System.Runtime.InteropServices;
using System.Reflection;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Sage 100 BOI: Get Next Order Number ===");
        
        // Configure these values or set environment variables
        string sage100Path = Environment.GetEnvironmentVariable("SAGE_SERVER_PATH") ?? @"\\uaf-erp\Sage Premium 2022\MAS90\Home";
        string username = Environment.GetEnvironmentVariable("SAGE_USERNAME") ?? "YOUR_USERNAME";
        string password = Environment.GetEnvironmentVariable("SAGE_PASSWORD") ?? "YOUR_PASSWORD";
        string companyCode = Environment.GetEnvironmentVariable("SAGE_COMPANY") ?? "TST";

        object pvx = null;
        object session = null;
        object salesOrder = null;

        try
        {
            // 1. Init ProvideX
            Type pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            if (pvxType == null) throw new Exception("ProvideX.Script not found");
            pvx = Activator.CreateInstance(pvxType);
            pvxType.InvokeMember("Init", BindingFlags.InvokeMethod, null, pvx, new object[] { sage100Path });

            // 2. Create Session
            session = pvxType.InvokeMember("NewObject", BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
            Type sessionType = session.GetType();
            
            // 3. Login
            object userRet = sessionType.InvokeMember("nSetUser", BindingFlags.InvokeMethod, null, session, new object[] { username, password });
            if (userRet.ToString() == "0") throw new Exception("Login failed: " + GetLastError(session));

            // 4. Set Company
            object compRet = sessionType.InvokeMember("nSetCompany", BindingFlags.InvokeMethod, null, session, new object[] { companyCode });
            if (compRet.ToString() == "0") throw new Exception("SetCompany failed: " + GetLastError(session));

            // 5. Set Module/Date
            sessionType.InvokeMember("nSetModule", BindingFlags.InvokeMethod, null, session, new object[] { "S/O" });
            sessionType.InvokeMember("nSetDate", BindingFlags.InvokeMethod, null, session, new object[] { "S/O", DateTime.Now.ToString("yyyyMMdd") });

            // 6. Set Program Context (Task)
            object taskId = sessionType.InvokeMember("nLookupTask", BindingFlags.InvokeMethod, null, session, new object[] { "SO_SalesOrder_ui" });
            sessionType.InvokeMember("nSetProgram", BindingFlags.InvokeMethod, null, session, new object[] { taskId });

            // 7. Create Business Object
            Console.WriteLine("Creating SO_SalesOrder_bus object...");
            salesOrder = pvxType.InvokeMember("NewObject", BindingFlags.InvokeMethod, null, pvx, new object[] { "SO_SalesOrder_bus", session });
            
            if (salesOrder == null) throw new Exception("Failed to create SO_SalesOrder_bus");
            Console.WriteLine("Business object created successfully.");

            // 8. Get Next Sales Order Number
            // nGetNextSalesOrderNo takes a variable by reference to return the value
            Console.WriteLine("Retrieving next Sales Order Number...");
            
            object[] args = new object[] { "" }; // Placeholder for return value
            ParameterModifier[] modifiers = new ParameterModifier[1];
            modifiers[0] = new ParameterModifier(1);
            modifiers[0][0] = true; // First argument is ByRef

            object result = salesOrder.GetType().InvokeMember(
                "nGetNextSalesOrderNo", 
                BindingFlags.InvokeMethod, 
                null, 
                salesOrder, 
                args, 
                modifiers, 
                null, 
                null
            );

            string nextOrderNo = args[0].ToString();
            
            Console.WriteLine("========================================");
            Console.WriteLine("NEXT SALES ORDER NUMBER: " + nextOrderNo);
            Console.WriteLine("========================================");

        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.Message);
        }
        finally
        {
            if (salesOrder != null) Marshal.ReleaseComObject(salesOrder);
            if (session != null) Marshal.ReleaseComObject(session);
            if (pvx != null) Marshal.ReleaseComObject(pvx);
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static string GetLastError(object session)
    {
        try {
            return (string)session.GetType().InvokeMember("sLastErrorMsg", BindingFlags.GetProperty, null, session, null);
        } catch { return "Unknown error"; }
    }
}
