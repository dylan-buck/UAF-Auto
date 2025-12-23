============================================
UAF SAGE MIDDLEWARE
Version 1.0
============================================

WHAT IS THIS?
-------------
This Windows Service automatically processes sales orders 
for Sage 100 via a REST API. It runs invisibly in the 
background and starts automatically when Windows boots.


REQUIREMENTS
------------
- Windows 10/11 or Windows Server 2016+
- .NET 8.0 Runtime (Desktop) - Download from:
  https://dotnet.microsoft.com/download/dotnet/8.0
- Sage 100 workstation components installed
- Network access to Sage 100 server


INSTALLATION
------------
1. Install .NET 8.0 Runtime if not already installed
2. Extract all files to: C:\UAF-Middleware\
3. Edit appsettings.json if needed (credentials, paths)
4. Right-click "install-service.bat" > Run as administrator
5. Done! Service is now running.


TESTING
-------
Open a browser to: http://localhost:3000/health
Should show: {"status":"healthy"...}


BATCH FILES
-----------
install-service.bat   - Install and start the service (Run as Admin)
uninstall-service.bat - Remove the service (Run as Admin)
start-service.bat     - Start the service (Run as Admin)
stop-service.bat      - Stop the service (Run as Admin)
view-logs.bat         - Open the logs folder
check-status.bat      - Check if service is running


CONFIGURATION
-------------
Edit appsettings.json to change:

  "Sage": {
    "Username": "YOUR_USERNAME",   <- Sage 100 username
    "Password": "YOUR_PASSWORD",   <- Sage 100 password
    "Company": "TST",              <- Company code (TST or UAF)
    "ServerPath": "\\\\uaf-erp\\Sage Premium 2022\\MAS90\\Home"
  },
  "Api": {
    "Port": 3000,             <- API port
    "ApiKey": ""              <- Optional API key for security
  }

After changes, restart the service:
  1. Run stop-service.bat (as Admin)
  2. Run start-service.bat (as Admin)


API ENDPOINTS
-------------
POST /api/v1/sales-orders
  - Create a new sales order
  - Headers: Content-Type: application/json
  - Headers: X-API-Key: <your-key> (if configured)
  
  Body:
  {
    "customerNumber": "CUST001",
    "poNumber": "PO-12345",
    "lines": [
      {
        "itemCode": "ITEM001",
        "quantity": 10
      }
    ]
  }

GET /health
  - Basic health check

GET /health/ready
  - Detailed health check (includes Sage 100 status)


LOGS
----
Logs are stored in the "logs" subfolder.
- uaf-middleware-YYYY-MM-DD.log (daily rolling)
- Also logged to Windows Event Viewer (Warnings/Errors)


TROUBLESHOOTING
---------------
1. Service won't start?
   - Check logs folder for error details
   - Check Windows Event Viewer > Application
   - Verify Sage 100 credentials in appsettings.json
   - Verify network path to Sage 100 server

2. API not responding?
   - Run check-status.bat
   - Check if another app is using port 3000
   - Check Windows Firewall settings

3. Orders failing?
   - Check logs for specific error messages
   - Verify customer/item codes exist in Sage 100
   - Verify user has Sales Order permissions


SUPPORT
-------
Contact: Dylan Buck
Project: UAF Air Filter Distribution


============================================




