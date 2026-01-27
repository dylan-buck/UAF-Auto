# UAF Middleware Configuration

## Setting Credentials

The middleware reads configuration from **environment variables** or a local settings file. 
**Never commit credentials to Git!**

### Option 1: Environment Variables (Recommended for Production)

Set these environment variables on the workstation:

```cmd
setx Sage__Username "your_sage_username"
setx Sage__Password "your_sage_password"
setx Sage__Company "TST"
setx Api__ApiKey "your_secret_api_key"
```

Or for the current session only:
```cmd
set Sage__Username=your_sage_username
set Sage__Password=your_sage_password
```

### Option 2: Local Settings File

Create a file called `appsettings.Local.json` in the same folder as the executable:

```json
{
  "Sage": {
    "Username": "your_sage_username",
    "Password": "your_sage_password",
    "Company": "TST"
  },
  "Api": {
    "ApiKey": "your_secret_api_key"
  }
}
```

This file is in `.gitignore` and won't be committed.

### Option 3: Edit appsettings.json After Download

After downloading/building, edit `appsettings.json` directly with your credentials.
Just don't commit it back to Git!

## Configuration Reference

| Setting | Environment Variable | Description |
|---------|---------------------|-------------|
| `Sage.Username` | `Sage__Username` | Sage 100 username |
| `Sage.Password` | `Sage__Password` | Sage 100 password |
| `Sage.Company` | `Sage__Company` | Company code (TST or UAF) |
| `Sage.ServerPath` | `Sage__ServerPath` | UNC path to Sage 100 |
| `Api.Port` | `Api__Port` | API port (default: 3000) |
| `Api.ApiKey` | `Api__ApiKey` | Optional API key for security |

## Testing Configuration

After configuring, test with:
```
http://localhost:3000/health/ready
```

If Sage 100 connection is successful, you'll see:
```json
{"status":"ready","sage100":"connected"}
```


