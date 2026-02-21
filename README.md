# UAF-Auto

Middleware and tooling for creating Sage 100 sales orders from external purchase order data.

## Repo Layout

- `api/`: Node.js API for inbound order requests and job tracking
- `sage-boi-service/`: .NET BOI service used by the Node API
- `UAFMiddleware/`: Windows-service/API implementation used for workstation-style deployments
- `po-upload-app/`: React upload/logs UI
- `docker-compose.yml`: local container stack for `api` + `sage-boi-service` + `redis`

## Prerequisites (Docker Stack)

- Docker + Docker Compose
- Network access to Sage 100 server path
- Sage credentials and company code

## Quick Start (Docker Stack)

1. Create env file.

```bash
cp .env.example .env
```

2. Fill required values in `.env` (at minimum: `API_KEY`, `SAGE_USERNAME`, `SAGE_PASSWORD`, `SAGE_COMPANY`, `SAGE_SERVER_PATH`).

3. Start services.

```bash
docker-compose up -d --build
```

4. Verify health.

```bash
curl http://localhost:3000/health
curl http://localhost:3000/health/ready
```

## API Endpoints (Node API)

- `POST /api/v1/sales-orders`
- `GET /api/v1/sales-orders/:jobId`
- `GET /health`
- `GET /health/ready`

All sales order routes require header `X-API-Key`.

Example request:

```bash
curl -X POST http://localhost:3000/api/v1/sales-orders \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your_api_key_here" \
  -d @api/tests/fixtures/sample-order.json
```

## Useful Commands

```bash
# Service logs
docker-compose logs -f

# Targeted logs
docker-compose logs -f api
docker-compose logs -f sage-boi-service
docker-compose logs -f redis
```

## Notes

- The BOI service endpoint used by the Node API is `POST /api/salesorder`.
- Windows service deployment and workstation scripts remain under `UAFMiddleware/`.
- This README is intentionally concise; subproject-specific details live with each subproject.
