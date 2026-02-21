# UAF-Auto

Middleware and automation tooling for creating Sage 100 sales orders from external purchase order data.

## Deployment Modes

- `Comp-08 Windows service` (current production path): `UAFMiddleware/`
- `Docker stack` (dev/integration path): `api/` + `sage-boi-service/` + `redis`

## Repo Layout

- `UAFMiddleware/`: production Windows service, service scripts, ops scripts, and wrappers
- `api/`: Node.js API for inbound order requests and job tracking
- `sage-boi-service/`: .NET BOI service used by the Node API
- `po-upload-app/`: React upload/logs UI
- `n8n/`: automation workflow exports/context
- `docker-compose.yml`: local container stack for `api` + `sage-boi-service` + `redis`

## Comp-08 Operations (Production)

Use these wrappers for normal operations:

- `uaf-bootstrap.cmd`
- `uaf-verify.cmd`
- `uaf-update.cmd`
- `uaf-set-company.cmd`
- `uaf-set-credentials.cmd`
- `run-test.bat`

Admin rights are required for bootstrap/update/profile/credential changes.

### PowerShell Command Rule

PowerShell does not run local scripts by name alone.

Use one of:
- `.\UAFMiddleware\uaf-verify.cmd` (relative path from repo root)
- `C:\UAF-Auto\UAFMiddleware\uaf-verify.cmd` (full path from anywhere)

Do not use just `uaf-verify.cmd` unless the directory is already in `PATH`.

### First-Time Setup or After Major Changes

From Comp-08:

```powershell
cd C:\UAF-Auto
git pull
C:\UAF-Auto\UAFMiddleware\uaf-bootstrap.cmd
C:\UAF-Auto\UAFMiddleware\uaf-verify.cmd
C:\UAF-Auto\UAFMiddleware\run-test.bat
```

`uaf-bootstrap.cmd` configures:
- `UAFSageMiddleware` startup + recovery actions
- `Cloudflared` startup type
- startup scheduled task `UAF-VerifyServices`
- post-bootstrap verification checks

### Reboot Recovery Validation

After reboot, run:

```powershell
C:\UAF-Auto\UAFMiddleware\uaf-verify.cmd
```

Expected:
- middleware service is `Running`
- cloudflared service is `Running`
- startup task has a recent `LastRunTime`
- startup task `result: 0`
- local and tunnel `/health/ready` checks pass

Note:
- `result: 267011` (`0x41303`) means the task is registered and ready but has not run yet (typically before first reboot).

### Ongoing Update Workflow

From the middleware directory:

```powershell
cd C:\UAF-Auto\UAFMiddleware
.\uaf-update.cmd
```

Optional pull + update:

```powershell
.\uaf-update.cmd -PullLatest
```

`uaf-update.cmd` behavior:
- backup current install
- build/publish
- stop service
- deploy binaries
- restart service
- local+tunnel health checks
- rollback on failure

## Production Logs and Health Endpoints

- Middleware logs: `C:\UAF-Auto\UAFMiddleware\logs\uaf-middleware-*.log`
- Boot verification log: `C:\UAF-Auto\logs\boot-verify.log`
- Ops logs: `C:\UAF-Auto\logs\ops\*.log`

Health endpoints:
- `http://localhost:3000/health`
- `http://localhost:3000/health/ready`
- `https://sage.uaf-automation.uk/health/ready`

## Docker Stack (Dev/Integration)

### Prerequisites

- Docker + Docker Compose
- Network access to Sage 100 server path
- Sage credentials and company code

### Quick Start

1. Create env file.

```bash
cp .env.example .env
```

2. Fill required values in `.env`:
- `API_KEY`
- `SAGE_USERNAME`
- `SAGE_PASSWORD`
- `SAGE_COMPANY`
- `SAGE_SERVER_PATH`

3. Start services.

```bash
docker-compose up -d --build
```

4. Verify health.

```bash
curl http://localhost:3000/health
curl http://localhost:3000/health/ready
```

### API Endpoints (Node API)

- `POST /api/v1/sales-orders`
- `GET /api/v1/sales-orders/:jobId`
- `GET /health`
- `GET /health/ready`

All sales order routes require header `X-API-Key`.

Example:

```bash
curl -X POST http://localhost:3000/api/v1/sales-orders \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your_api_key_here" \
  -d @api/tests/fixtures/sample-order.json
```

## Related Docs

- `UAFMiddleware/README.txt`
- `UAFMiddleware/CONFIGURE.md`
- `UAFMiddleware/DEPLOYMENT-GUIDE.md`
