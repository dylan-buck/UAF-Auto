# UAF Sage Middleware - Deployment Guide

This guide covers deployment and operations on Comp-08.

## Prerequisites
- Windows admin access
- .NET 10 runtime (and SDK if building on host)
- Sage workstation components / ProvideX
- cloudflared installed on host
- Access to the configured Sage path for the target environment

## Package Build
From repo `UAFMiddleware/`:

```cmd
build-package.bat
```

Output package:
- `UAF-Middleware-v1.0\`
- includes middleware binaries, source (`src\`), service scripts, ops scripts, wrapper commands, and cloudflared template

## First-Time Install (Comp-08)
1. Extract package to target directory (example: `C:\UAF-Middleware\UAF-Middleware-v1.0`).
2. Configure profile/credentials:

```cmd
uaf-set-company.cmd -Profile TST
```

3. Install middleware service (admin):

```cmd
install-service.bat
```

4. Run host bootstrap (admin):

```cmd
uaf-bootstrap.cmd
```

Bootstrap performs:
- service startup/recovery enforcement for middleware
- cloudflared service validation/startup configuration
- startup task registration (`UAF-VerifyServices`)
- verification checks

## Standard Operations

### Verify stack health
```cmd
uaf-verify.cmd
```

### Run API smoke test
```cmd
run-test.bat
```

### Update middleware safely
```cmd
uaf-update.cmd
```

Optional:
```cmd
uaf-update.cmd -PullLatest
```

Update behavior:
- backup current install
- build/publish
- stop service
- deploy binaries
- start service
- health checks (local + tunnel)
- rollback automatically on failure

Note: `uaf-update.cmd` builds from local `src\` and requires .NET SDK on host.

### Switch environment profile
```cmd
uaf-set-company.cmd -Profile TST
uaf-set-company.cmd -Profile UAF
```

This updates company + credentials and restarts service.

### Rotate credentials without changing profile
```cmd
uaf-set-credentials.cmd
```

## Reboot Recovery Model
- `UAFSageMiddleware` service set to `Automatic`
- cloudflared service set to `Automatic`
- `UAF-VerifyServices` scheduled task runs on startup as `SYSTEM`
- boot verifier checks and starts services as needed
- local and tunnel readiness checks are logged

## Verification After Reboot (Acceptance)
1. Reboot Comp-08.
2. Run:

```cmd
uaf-verify.cmd
```

3. Confirm:
- middleware service running
- cloudflared service running
- startup task exists and last result is success
- local readiness endpoint is ready
- tunnel readiness endpoint is ready

## Logs
- Middleware logs: `logs\uaf-middleware-*.log`
- Boot verifier: `C:\UAF-Auto\logs\boot-verify.log`
- Ops logs: `C:\UAF-Auto\logs\ops\*.log`

## Troubleshooting
1. Run `uaf-verify.cmd` first.
2. If cloudflared missing or failing, check host install and tunnel credentials path.
3. If middleware fails health checks, inspect middleware log + Windows Event Viewer.
4. Re-run `uaf-bootstrap.cmd` after environment changes.
