using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Sage 100 BOI Path Check ===");
        Console.WriteLine("");

        // Check ODBC DSN registry for the correct path
        Console.WriteLine("Step 1: Checking ODBC DSN registry...");
        try
        {
            // Try to find SOTAMAS90 or similar DSN
            RegistryKey odbcKey = Registry.CurrentUser.OpenSubKey(@"Software\ODBC\ODBC.INI");
            if (odbcKey != null)
            {
                string[] dsnNames = odbcKey.GetSubKeyNames();
                Console.WriteLine("Found DSNs:");
                foreach (string dsnName in dsnNames)
                {
                    if (dsnName.ToUpper().Contains("MAS") || dsnName.ToUpper().Contains("SAGE"))
                    {
                        Console.WriteLine("  - " + dsnName);
                        RegistryKey dsnKey = odbcKey.OpenSubKey(dsnName);
                        if (dsnKey != null)
                        {
                            object dirValue = dsnKey.GetValue("Directory");
                            if (dirValue != null)
                            {
                                string directory = dirValue.ToString();
                                Console.WriteLine("    Directory: " + directory);
                                string homePath = directory + "\\Home";
                                Console.WriteLine("    Home Path: " + homePath);
                                Console.WriteLine("");
                                Console.WriteLine("Try using this path in your Init() call:");
                                Console.WriteLine("  " + homePath);
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Could not access ODBC registry");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading registry: " + ex.Message);
        }

        Console.WriteLine("");
        Console.WriteLine("Step 2: Testing with current path...");
        string currentPath = @"\\uaf-erp\Sage Premium 2022\MAS90\Home";
        Console.WriteLine("Current path: " + currentPath);
        Console.WriteLine("");

        // Test if we can access the path
        if (System.IO.Directory.Exists(currentPath))
        {
            Console.WriteLine("SUCCESS: Path exists and is accessible");
            
            // Check if SO_SalesOrder_bus.pvc exists
            string[] possibleFiles = {
                currentPath + "\\SO\\SO_SalesOrder_bus.pvc",
                currentPath + "\\SO_SalesOrder_bus.pvc",
                currentPath + "\\SO\\SO570_SalesOrder_BUS.PVC"
            };

            Console.WriteLine("");
            Console.WriteLine("Checking for business object files:");
            foreach (string filePath in possibleFiles)
            {
                if (System.IO.File.Exists(filePath))
                {
                    Console.WriteLine("  FOUND: " + filePath);
                }
                else
                {
                    Console.WriteLine("  NOT FOUND: " + filePath);
                }
            }
        }
        else
        {
            Console.WriteLine("ERROR: Path does not exist or is not accessible");
            Console.WriteLine("This might be why Error 90 occurs!");
        }

        Console.WriteLine("");
        Console.WriteLine("Step 3: Testing ProvideX Init with current path...");
        try
        {
            Type pvxType = Type.GetTypeFromProgID("ProvideX.Script");
            object pvx = Activator.CreateInstance(pvxType);
            pvxType.InvokeMember("Init", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { currentPath });
            Console.WriteLine("SUCCESS: ProvideX Init worked with current path");
            
            // Try to create session
            object session = pvxType.InvokeMember("NewObject", System.Reflection.BindingFlags.InvokeMethod, null, pvx, new object[] { "SY_Session" });
            Type sessionType = session.GetType();
            string username = Environment.GetEnvironmentVariable("SAGE_USERNAME") ?? "YOUR_USERNAME";
            string password = Environment.GetEnvironmentVariable("SAGE_PASSWORD") ?? "YOUR_PASSWORD";
            string company = Environment.GetEnvironmentVariable("SAGE_COMPANY") ?? "TST";
            sessionType.InvokeMember("nSetUser", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { username, password });
            sessionType.InvokeMember("nSetCompany", System.Reflection.BindingFlags.InvokeMethod, null, session, new object[] { company });
            Console.WriteLine("SUCCESS: Session created");

            // Check if we can get last error message
            try
            {
                object lastError = sessionType.InvokeMember("sLastErrorMsg", System.Reflection.BindingFlags.GetProperty, null, session, null);
                if (lastError != null)
                {
                    Console.WriteLine("Last error message: " + lastError.ToString());
                }
            }
            catch
            {
                Console.WriteLine("Could not read sLastErrorMsg property");
            }

            Marshal.ReleaseComObject(session);
            Marshal.ReleaseComObject(pvx);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
        }

        Console.WriteLine("");
        Console.WriteLine("========================================");
        Console.WriteLine("Path check complete.");
        Console.WriteLine("========================================");
        Console.WriteLine("");
        Console.WriteLine("IMPORTANT: Also check in Sage 100:");
        Console.WriteLine("  1. Company Setup -> Company -> 'Allow External Access' checkbox");
        Console.WriteLine("  2. User Security -> Ensure user has SO module permissions");
        Console.WriteLine("");

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

