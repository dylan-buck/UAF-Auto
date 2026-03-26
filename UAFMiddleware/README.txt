============================================
UAF SAGE MIDDLEWARE
Version 1.1
============================================

WHAT IS THIS?
-------------
This Windows Service processes Sage 100 sales orders via REST API.
It runs in the background and starts automatically on Windows boot.

This package now includes operational wrappers and host bootstrap scripts
for safer updates, reboot recovery checks, and profile management.


REQUIREMENTS
------------
- Windows 10/11 or Windows Server 2016+
- .NET 10.0 Runtime (Desktop)
- Sage 100 workstation components installed
- Network access to Sage 100 server
- cloudflared installed for external tunnel access


INSTALLATION (FIRST TIME)
-------------------------
1. Extract all files to: C:\UAF-Middleware\
2. Edit appsettings.Local.json or run uaf-set-company.cmd
3. Right-click install-service.bat > Run as administrator
4. Run uaf-bootstrap.cmd (as administrator) for full host hardening


PRIMARY OPERATOR COMMANDS
-------------------------
uaf-bootstrap.cmd        - Configure startup verifier + service dependencies
uaf-update.cmd           - Build, deploy, health-check, rollback on failure
uaf-set-company.cmd      - Switch profile (TST/UAF) and credentials
uaf-set-credentials.cmd  - Rotate credentials (keeps current profile unless provided)
uaf-verify.cmd           - Verify middleware + cloudflared + task + health

These wrappers call PowerShell scripts internally. Admin rights are auto-requested
for privileged actions.
uaf-update.cmd requires .NET SDK because it builds from local source.


LEGACY BATCH COMMANDS
---------------------
install-service.bat      - Install and start the middleware service (Admin)
uninstall-service.bat    - Remove middleware service (Admin)
start-service.bat        - Start middleware service (Admin)
stop-service.bat         - Stop middleware service (Admin)
check-status.bat         - Quick service + local health check
view-logs.bat            - Open logs folder
run-test.bat             - Run API smoke test script


REBOOT BEHAVIOR
---------------
- UAFSageMiddleware is configured as Automatic startup service.
- cloudflared should be installed as Automatic startup service.
- UAF-VerifyServices startup task runs after boot (SYSTEM account)
  and verifies/restarts middleware/cloudflared plus health endpoints.


PROFILE SWITCHING (TST/UAF)
---------------------------
Use:
  uaf-set-company.cmd -Profile TST
  uaf-set-company.cmd -Profile UAF

The command updates company + credentials together and restarts the service.
If username/password are omitted, it prompts interactively.


HEALTH ENDPOINTS
----------------
GET /health
GET /health/ready

Local check:
  http://localhost:3000/health/ready

Tunnel check:
  https://<public-health-host>/health/ready


LOGS
----
- Middleware logs: logs\uaf-middleware-YYYY-MM-DD.log
- Boot verification: C:\UAF-Auto\logs\boot-verify.log
- Ops scripts: C:\UAF-Auto\logs\ops\*.log


TROUBLESHOOTING QUICK CHECK
---------------------------
1. Run uaf-verify.cmd
2. Check middleware service status
3. Check cloudflared service status
4. Confirm UAF-VerifyServices task exists and has recent run
5. Check logs for exact error


SUPPORT
-------
Contact: Repository maintainer or owning team
Project: Sage 100 middleware

============================================
