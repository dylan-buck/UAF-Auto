# UAF Middleware Configuration

## Configuration Sources (priority)
1. Environment variables
2. `appsettings.Local.json`
3. `appsettings.json` (template defaults)

Do not store real credentials in `appsettings.json`.

## Recommended Operator Flow
Use wrapper commands instead of editing files manually:

```cmd
uaf-set-company.cmd -Profile TST
uaf-set-company.cmd -Profile UAF
uaf-set-credentials.cmd
```

These commands can:
- update Sage `Company` (`TST` or `UAF`)
- update Sage username/password
- optionally persist machine-level environment variables
- restart middleware and run readiness checks

## Environment Variables

```cmd
setx Sage__Username "your_sage_username"
setx Sage__Password "your_sage_password"
setx Sage__Company "TST"
setx Api__ApiKey "your_secret_api_key"
```

`Api__ApiKey` is the legacy full-access key. Prefer scoped keys for new clients.

## Local Settings File
Create `appsettings.Local.json` next to `UAFMiddleware.exe`:

```json
{
  "Sage": {
    "Username": "your_sage_username",
    "Password": "your_sage_password",
    "Company": "TST"
  },
  "Api": {
    "ApiKey": "your_legacy_full_access_key",
    "ReadOnlyMode": false,
    "ApiKeys": {
      "mcp_readonly_secret": {
        "Name": "mcp-readonly",
        "Scopes": [ "read" ]
      },
      "automation_create_secret": {
        "Name": "po-automation",
        "Scopes": [ "read", "create" ]
      },
      "finance_read_secret": {
        "Name": "finance-read",
        "Scopes": [ "read", "finance" ]
      }
    }
  }
}
```

Available scopes:
- `read`: non-financial lookup and query endpoints.
- `create`: create workflow endpoints such as sales-order creation.
- `modify`: future update/cancel endpoints.
- `finance`: financial read endpoints such as customer account summaries.
- `admin`: future catalog/admin endpoints.

Set `"ReadOnlyMode": true` to force every configured key to read-only behavior. This is useful for an MCP deployment that must not create or modify Sage records.

## Cloudflared Configuration
- Runtime config path on host: `C:\UAF-Auto\.cloudflared\config.yml`
- Template included in package: `cloudflared\config.template.yml`

## Validation Commands

```cmd
uaf-verify.cmd
check-status.bat
```

Manual health checks:
- Local: `http://localhost:3000/health/ready`
- Tunnel: `https://<public-health-host>/health/ready`
