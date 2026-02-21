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
    "ApiKey": "your_secret_api_key"
  }
}
```

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
- Tunnel: `https://sage.uaf-automation.uk/health/ready`
