# UAF Sage Middleware - Deployment Guide

This guide walks you through deploying the middleware on the Windows workstation.

## Folder Structure

```
UAFMiddleware/                  <- Copy this entire folder to workstation
├── src/                        # Source code (.NET project)
│   ├── UAFMiddleware.csproj
│   ├── Program.cs
│   ├── appsettings.json        # Configuration file
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   └── Configuration/
├── UAFMiddleware.sln           # Visual Studio solution
├── build-package.bat           # Step 1: Run this to build
├── install-service.bat         # Step 2: Run this to install
├── uninstall-service.bat
├── start-service.bat
├── stop-service.bat
├── view-logs.bat
├── check-status.bat
├── test-api.ps1                # PowerShell test script
└── DEPLOYMENT-GUIDE.md         # This file
```

## Prerequisites

- Windows 10/11 or Windows Server 2016+
- Administrator access on the workstation
- Network access to Sage 100 server (`\\uaf-erp\Sage Premium 2022\MAS90\Home`)
- Sage 100 workstation components installed (ProvideX)

## Step 1: Install .NET 8.0 SDK (for building)

> **Note:** You need the SDK to build. After deployment, you can uninstall the SDK and keep only the Runtime.

1. Go to: https://dotnet.microsoft.com/download/dotnet/8.0
2. Under ".NET 8.0", click **Windows** → **x64** → **SDK** (Download the installer)
3. Run the installer
4. Restart your command prompt/PowerShell after installation

To verify: Open Command Prompt and type:
```cmd
dotnet --version
```
Should show something like `8.0.xxx`

## Step 2: Copy UAFMiddleware Folder to Workstation

Copy the entire `UAFMiddleware` folder from the repository to the workstation.

Suggested location: `C:\UAF-Middleware\`

You can:
- Copy via network share
- Copy via USB drive
- Clone the Git repository directly

## Step 3: Build the Package

1. Open Command Prompt **as Administrator**
2. Navigate to the folder:
   ```cmd
   cd C:\UAF-Middleware\UAFMiddleware
   ```
3. Run the build script:
   ```cmd
   build-package.bat
   ```

This will create a `UAF-Middleware-v1.0` folder with the compiled application.

## Step 4: Configure the Application

Before installing, edit the configuration:

1. Open `UAF-Middleware-v1.0\appsettings.json` in Notepad
2. Verify/update the Sage settings:

```json
{
  "Sage": {
    "Username": "YOUR_SAGE_USERNAME",
    "Password": "YOUR_SAGE_PASSWORD",
    "Company": "TST",
    "ServerPath": "\\\\uaf-erp\\Sage Premium 2022\\MAS90\\Home"
  },
  "Api": {
    "Port": 3000,
    "ApiKey": ""
  }
}
```

- **Username/Password**: Sage 100 credentials
- **Company**: `TST` for testing, `UAF` for production
- **ServerPath**: UNC path to Sage 100 (use double backslashes in JSON)
- **ApiKey**: Leave empty for now, or set a secret key for security

## Step 5: Install the Windows Service

1. Open the `UAF-Middleware-v1.0` folder
2. **Right-click** `install-service.bat`
3. Select **"Run as administrator"**
4. Wait for "SUCCESS" message

The service will:
- Install automatically
- Start automatically
- Restart on Windows boot
- Restart if it crashes

## Step 6: Verify Installation

### Check Service Status
```cmd
check-status.bat
```

### Test Health Endpoint
Open a browser to: http://localhost:3000/health

Should show:
```json
{"status":"healthy","version":"1.0.0",...}
```

### Test Sage 100 Connection
Open: http://localhost:3000/health/ready

Should show:
```json
{"status":"ready","sage100":"connected",...}
```

## Step 7: Test Order Creation

Before testing, you need real data from Sage 100:

1. **Get a Customer Number**: Open Sage 100 → A/R → Customer Maintenance → Note any customer number
2. **Get an Item Code**: Open Sage 100 → I/M → Item Maintenance → Note any item code

Then test with PowerShell:
```powershell
.\test-api.ps1 -CustomerNumber "YOURCUST" -ItemCode "YOURITEM"
```

Or manually:
```powershell
$body = @{
    customerNumber = "YOURCUST"
    poNumber = "TEST-001"
    lines = @(
        @{
            itemCode = "YOURITEM"
            quantity = 1
        }
    )
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:3000/api/v1/sales-orders" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

## Managing the Service

| Task | Command |
|------|---------|
| Start | `start-service.bat` (as Admin) |
| Stop | `stop-service.bat` (as Admin) |
| Uninstall | `uninstall-service.bat` (as Admin) |
| View Logs | `view-logs.bat` |
| Check Status | `check-status.bat` |

Or use Windows Services:
1. Press `Win + R`
2. Type `services.msc`
3. Find "UAF Sage Middleware"

## Switching to Production

When ready to use with real orders:

1. Stop the service: `stop-service.bat`
2. Edit `appsettings.json`:
   - Change `"Company": "TST"` to `"Company": "UAF"`
   - Optionally set an `"ApiKey"` for security
3. Start the service: `start-service.bat`

## Troubleshooting

### Service won't start
1. Check logs: `view-logs.bat`
2. Check Windows Event Viewer (Application log)
3. Verify Sage 100 path is accessible
4. Verify credentials are correct

### API not responding
1. Check if service is running: `check-status.bat`
2. Check if port 3000 is blocked by firewall
3. Check logs for errors

### Order creation fails
1. Verify customer number exists in Sage 100
2. Verify item code exists in Sage 100
3. Verify user has Sales Order permissions
4. Check logs for detailed error message

## Logs Location

- Application logs: `UAF-Middleware-v1.0\logs\`
- Windows Event Log: Event Viewer → Windows Logs → Application

## Uninstalling

1. Run `uninstall-service.bat` as Administrator
2. Delete the `UAF-Middleware-v1.0` folder
3. Optionally uninstall .NET SDK if no longer needed
