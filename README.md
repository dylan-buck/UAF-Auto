# UAF-Auto

Automation and middleware for turning inbound purchase order data into Sage 100 sales orders.

This repository currently supports two operating paths:

- `UAFMiddleware/`: Windows service and operator wrappers used on the host machine.
- `api/` + `sage-boi-service/` + `redis`: Docker-based API stack used for local development and integration work.

Supporting assets live in:

- `po-upload-app/`: React upload and logs UI
- `n8n/`: workflow assets and transformation scripts
- `UAFMiddleware/docs/`: Windows service design and deployment docs

## Repo Layout

- `UAFMiddleware/`: production Windows service, wrapper commands, bootstrap/update scripts, and packaging assets
- `api/`: Node.js API for inbound sales-order requests and job tracking
- `sage-boi-service/`: .NET BOI service used by the Node API
- `po-upload-app/`: Vite/React UI for PO upload and upload-history viewing
- `n8n/`: prompt assets and code used by the n8n workflow layer
- `docker-compose.yml`: local stack for `api`, `sage-boi-service`, and `redis`
- `start_all_native.bat`: local native startup helper
- `verify_deployment.bat`: local Docker deployment smoke-test helper

## Runtime-Only Config

Do not commit machine-specific or secret-bearing runtime files. The repository is set up so these stay local:

- `.env`
- `po-upload-app/.env`
- `.cloudflared/config.yml`
- `.cloudflared/*.json`
- `appsettings.Local.json`

Before moving this repo to a work-owned organization, replace or rotate any personal or single-user infrastructure references:

- API keys
- Sage credentials
- Cloudflare tunnel IDs and credentials
- frontend `VITE_*` webhook URLs
- any personal usernames or workstation-specific paths

## Docker Stack (Dev / Integration)

### Prerequisites

- Docker Desktop or Docker Engine with Compose support
- Network access to the Sage 100 installation path
- Valid Sage credentials and company code
- A configured frontend webhook destination if you plan to run `po-upload-app`

### Backend Quick Start

1. Copy the root environment template.

```bash
cp .env.example .env
```

2. Fill in the required values in `.env`.

Required values:

- `API_KEY`
- `SAGE_USERNAME`
- `SAGE_PASSWORD`
- `SAGE_COMPANY`
- `SAGE_SERVER_PATH`

3. Start the services.

```bash
docker compose up -d --build
```

4. Verify health.

```bash
curl http://localhost:3000/health
curl http://localhost:3000/health/ready
```

### Frontend Quick Start

The upload UI talks to n8n webhooks directly. It does not proxy through the Node API.

1. Copy the frontend environment template.

```bash
cp po-upload-app/.env.example po-upload-app/.env
```

2. Set:

- `VITE_N8N_WEBHOOK_URL`
- `VITE_N8N_HISTORY_URL`

3. Start the frontend.

```bash
cd po-upload-app
npm install
npm run dev
```

If those `VITE_*` variables are not set, the UI now fails closed with a configuration error instead of calling a baked-in personal endpoint.

## API Endpoints

Node API routes:

- `POST /api/v1/sales-orders`
- `GET /api/v1/sales-orders/:jobId`
- `GET /health`
- `GET /health/ready`

All sales-order routes require header `X-API-Key`.

Example:

```bash
curl -X POST http://localhost:3000/api/v1/sales-orders \
  -H "Content-Type: application/json" \
  -H "X-API-Key: <your-api-key>" \
  -d @api/tests/fixtures/sample-order.json
```

## Comp-08 Operations (Windows Service Path)

Use these wrappers for normal operations:

- `uaf-bootstrap.cmd`
- `uaf-verify.cmd`
- `uaf-update.cmd`
- `uaf-set-company.cmd`
- `uaf-set-credentials.cmd`
- `run-test.bat`

Admin rights are required for bootstrap, update, profile changes, and credential rotation.

### PowerShell Command Rule

PowerShell does not run local scripts by name alone.

Use one of:

- `.\UAFMiddleware\uaf-verify.cmd`
- `C:\UAF-Auto\UAFMiddleware\uaf-verify.cmd`

Do not rely on `uaf-verify.cmd` unless the directory is already in `PATH`.

### First-Time Setup or After Major Changes

From the host machine:

```powershell
cd C:\UAF-Auto
git pull
C:\UAF-Auto\UAFMiddleware\uaf-bootstrap.cmd
C:\UAF-Auto\UAFMiddleware\uaf-verify.cmd
C:\UAF-Auto\UAFMiddleware\run-test.bat
```

`uaf-bootstrap.cmd` configures:

- `UAFSageMiddleware` startup and recovery actions
- `cloudflared` startup type
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
- startup task result is `0`
- local and tunnel `/health/ready` checks pass

Note:

- `267011` (`0x41303`) means the task is registered and ready but has not run yet

### Ongoing Update Workflow

```powershell
cd C:\UAF-Auto\UAFMiddleware
.\uaf-update.cmd
```

Optional pull and update:

```powershell
.\uaf-update.cmd -PullLatest
```

`uaf-update.cmd` behavior:

- backup current install
- build and publish
- stop service
- deploy binaries
- restart service
- run local and tunnel health checks
- rollback on failure

## Logs and Health

Windows service logs:

- `C:\UAF-Auto\UAFMiddleware\logs\uaf-middleware-*.log`
- `C:\UAF-Auto\logs\boot-verify.log`
- `C:\UAF-Auto\logs\ops\*.log`

Health endpoints:

- `http://localhost:3000/health`
- `http://localhost:3000/health/ready`
- `https://<public-health-host>/health/ready` when a Cloudflare tunnel is configured

## Related Docs

- `UAFMiddleware/README.txt`
- `UAFMiddleware/CONFIGURE.md`
- `UAFMiddleware/DEPLOYMENT-GUIDE.md`
- `UAFMiddleware/docs/API_DESIGN.md`
- `UAFMiddleware/cloudflared/config.template.yml`
