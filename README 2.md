# UAF Air Filter Distribution - Sage 100 BOI Middleware

Automated sales order creation system for commercial air filter distribution. Integrates n8n workflow automation with Sage 100 Premium 2022 ERP via Business Object Interface (BOI).

## 🎯 Overview

This middleware provides a REST API that accepts sales order requests and creates them in Sage 100 ERP using the ProvideX Business Object Interface.

**Volume:** ~100 orders/month (3-5 per day)  
**Architecture:** Simplified for low-volume, high-reliability  
**Components:** Node.js API + .NET BOI Service + Redis

## 📋 Prerequisites

- **Docker & Docker Compose**: Required for running the services
- **Sage 100 Premium 2022**: On-premise installation
- **Network Access**: The server must have network access to the Sage 100 server (via UNC path)
- **ProvideX COM**: Must be registered on the Windows machine (included with Sage 100 client)
- **Sage 100 User**: User account with Sales Order module permissions

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone <repository-url>
cd UAF-Auto
```

### 2. Configure Environment Variables

```bash
cp .env.example .env
```

Edit `.env` and update the following variables:

```env
# API Configuration
API_KEY=your_secure_api_key_here

# Sage 100 Configuration
SAGE_USERNAME=dyl
SAGE_PASSWORD=your_password
SAGE_COMPANY=MAS_TST
SAGE_SERVER_PATH=\\uaf-erp\Sage Premium 2022\MAS90\Home
```

### 3. Start the Services

```bash
docker-compose up -d
```

### 4. Check Health Status

```bash
curl http://localhost:3000/health
curl http://localhost:3000/health/ready
```

## 📖 API Documentation

### Authentication

All API requests require an API key in the `X-API-Key` header:

```bash
curl -H "X-API-Key: your_api_key_here" http://localhost:3000/api/v1/sales-orders
```

### Endpoints

#### `POST /api/v1/sales-orders`

Create a new sales order.

**Request Body:**

```json
{
  "customerNumber": "CUST001",
  "poNumber": "PO-2025-001",
  "orderDate": "20251107",
  "comment": "Test order",
  "shipToAddress": {
    "name": "Customer Name",
    "address1": "123 Main St",
    "city": "Dallas",
    "state": "TX",
    "zipCode": "75201"
  },
  "lines": [
    {
      "itemCode": "FILTER-001",
      "quantity": 10,
      "unitPrice": 25.50
    }
  ],
  "async": false,
  "priority": "normal"
}
```

**Synchronous Response (async: false):**

```json
{
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "completed",
  "salesOrderNumber": "SO-12345",
  "message": "Order created successfully"
}
```

**Asynchronous Response (async: true):**

```json
{
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "queued",
  "message": "Order queued for processing"
}
```

#### `GET /api/v1/sales-orders/:jobId`

Get the status of a job.

**Response:**

```json
{
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "completed",
  "createdAt": "2025-11-07T14:23:05Z",
  "processedAt": "2025-11-07T14:23:08Z",
  "result": {
    "success": true,
    "salesOrderNumber": "SO-12345"
  }
}
```

#### `GET /health`

Basic health check.

#### `GET /health/ready`

Readiness check (includes Redis and BOI service connectivity).

## 🧪 Testing

### Run Test Script

```bash
cd api
chmod +x tests/test-api.sh
./tests/test-api.sh
```

### Manual Testing with curl

```bash
# Test with sample order
curl -X POST http://localhost:3000/api/v1/sales-orders \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your_api_key_here" \
  -d @api/tests/fixtures/sample-order.json
```

## 🛠️ Development

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api
docker-compose logs -f sage-boi-service
docker-compose logs -f redis
```

### Restart Services

```bash
docker-compose restart api
docker-compose restart sage-boi-service
```

### Rebuild After Code Changes

```bash
docker-compose up -d --build
```

## 📁 Project Structure

```
UAF-Auto/
├── api/                          # Node.js API Service
│   ├── src/
│   │   ├── controllers/          # Request handlers
│   │   ├── services/             # Business logic
│   │   ├── middleware/           # Express middleware
│   │   ├── routes/               # API routes
│   │   ├── config/               # Configuration
│   │   └── utils/                # Utilities (logger)
│   ├── tests/                    # Test fixtures
│   ├── Dockerfile
│   └── package.json
│
├── sage-boi-service/             # .NET BOI Integration
│   ├── SageBOI.Api/
│   │   ├── Controllers/          # API controllers
│   │   ├── Services/             # BOI services
│   │   ├── Models/               # DTOs
│   │   └── Configuration/        # Config classes
│   ├── Dockerfile
│   └── SageBOI.sln
│
├── docs/
│   └── ARCHITECTURE.md           # Detailed architecture docs
│
├── .env.example
├── docker-compose.yml
└── README.md
```

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `API_PORT` | API server port | `3000` |
| `API_KEY` | API authentication key | Required |
| `SAGE_USERNAME` | Sage 100 username | Required |
| `SAGE_PASSWORD` | Sage 100 password | Required |
| `SAGE_COMPANY` | Company code (MAS_TST or MAS_UAF) | Required |
| `SAGE_SERVER_PATH` | UNC path to Sage 100 server | Required |
| `BOI_SESSION_POOL_SIZE` | Number of BOI sessions | `3` |
| `REDIS_HOST` | Redis hostname | `redis` |
| `QUEUE_CHECK_INTERVAL` | Queue check interval (ms) | `10000` |
| `QUEUE_MAX_RETRIES` | Max retry attempts | `3` |
| `JOB_COMPLETED_TTL` | Completed job TTL (seconds) | `604800` (7 days) |

## 📊 Monitoring

### Check Queue Depth

```bash
docker-compose exec redis redis-cli
> LLEN sage:queue:normal
> LLEN sage:queue:high
```

### View Failed Jobs

```bash
docker-compose exec redis redis-cli KEYS "job:*"
docker-compose exec redis redis-cli HGETALL "job:abc123"
```

### View Logs

Logs are stored in `logs/` directory:
- `logs/api/` - API service logs
- `logs/boi-service/` - BOI service logs

## 🚨 Troubleshooting

### BOI Service Not Ready

**Issue:** `/health/ready` returns 503

**Solutions:**
1. Check Sage 100 server is accessible: `Test-Path "\\uaf-erp\Sage Premium 2022\MAS90\Home"`
2. Verify user has SO module permissions in Sage 100
3. Check BOI service logs: `docker-compose logs sage-boi-service`

### Jobs Stuck in Queue

**Issue:** Jobs remain in "queued" status

**Solutions:**
1. Check BOI service is running: `docker-compose ps`
2. Check queue processor logs: `docker-compose logs api | grep processor`
3. Restart API service: `docker-compose restart api`

### COM Error 90 (NewObject)

**Issue:** Cannot create SO_SalesOrder_bus object

**Causes:**
- User lacks SO module permissions
- Company code incorrect
- Module not licensed/installed

**Solution:**
Contact Sage 100 administrator to verify permissions and module activation.

## 🔐 Security

- API key authentication for all endpoints
- Environment variables for sensitive configuration
- No database of sensitive data (Redis only for job tracking)
- Logs do not contain passwords or sensitive data

## 📝 Notes

### Testing vs Production

- **Testing:** Use `SAGE_COMPANY=MAS_TST`
- **Production:** Use `SAGE_COMPANY=MAS_UAF`
- Update `API_KEY` to a strong random value for production

### Deployment Considerations

- Run on Windows server or workstation with Sage 100 client installed
- Ensure ProvideX COM is registered on the host system
- Network access to Sage 100 server required
- Consider firewall rules for Docker containers

## 📚 Additional Documentation

See `docs/ARCHITECTURE.md` for detailed architecture information.

## 🤝 Support

For issues or questions:
1. Check the troubleshooting section
2. Review logs in `logs/` directory
3. Check Sage 100 user permissions

## 📄 License

[Your License Here]

