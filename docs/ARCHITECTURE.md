# UAF Air Filter Distribution - Sage 100 BOI Middleware

## Project Overview

Automated sales order creation system for commercial air filter distribution company. Integrates n8n workflow automation with Sage 100 Premium 2022 ERP via Business Object Interface (BOI).

**Volume:** ~100 orders/month (3-5 per day) - Optimized for low-volume, high-reliability  
**Status:** Development Phase  
**Test Environment:** MAS_TST Company  
**Production Environment:** MAS_UAF Company  
**Last Updated:** November 7, 2025

---

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Core Components](#core-components)
5. [High Availability Design](#high-availability-design)
6. [Security](#security)
7. [API Specification](#api-specification)
8. [Development Workflow](#development-workflow)
9. [Deployment](#deployment)
10. [Monitoring](#monitoring)
11. [Troubleshooting](#troubleshooting)

---

## System Architecture

### High-Level Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                         EMAIL INBOX                              │
│              (Purchase Orders as PDF attachments)                │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                         N8N WORKFLOW                             │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐  │
│  │ Email Trigger│→ │ Extract PDF  │→ │ Parse PO Data       │  │
│  │ (IMAP/POP3)  │  │ Attachment   │  │ (AI/OCR/Template)   │  │
│  └──────────────┘  └──────────────┘  └──────────┬──────────┘  │
│                                                   │              │
│                                                   ▼              │
│                                      ┌─────────────────────┐    │
│                                      │ Validate & Format   │    │
│                                      │ Order Data          │    │
│                                      └──────────┬──────────┘    │
└─────────────────────────────────────────────────┼──────────────┘
                                                   │
                                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MIDDLEWARE API (Your Code)                    │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              REST API (ASP.NET Core / Node.js)            │  │
│  │  ┌────────────┐  ┌────────────┐  ┌──────────────────┐   │  │
│  │  │ Validation │  │ Queue Mgmt │  │  BOI Integration │   │  │
│  │  │  Layer     │→ │ (RabbitMQ/ │→ │     Layer        │   │  │
│  │  │            │  │  Redis)    │  │                  │   │  │
│  │  └────────────┘  └────────────┘  └────────┬─────────┘   │  │
│  └──────────────────────────────────────────────┼───────────┘  │
└─────────────────────────────────────────────────┼──────────────┘
                                                   │
                                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SAGE 100 BOI LAYER                          │
│  ┌────────────────────────────────────────────────────────┐    │
│  │   ProvideX Session Manager (Connection Pool)           │    │
│  │   ┌──────────────┐  ┌──────────────┐  ┌─────────────┐ │    │
│  │   │ Authenticate │→ │ Set Company  │→ │ Create SO   │ │    │
│  │   │ & Session    │  │ Context      │  │ Business Obj│ │    │
│  │   └──────────────┘  └──────────────┘  └─────────────┘ │    │
│  └────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                                                   │
                                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                    SAGE 100 PREMIUM 2022                         │
│              \\uaf-erp\Sage Premium 2022\MAS90\                 │
│         Dev: MAS_TST  |  Prod: MAS_UAF                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Technology Stack

### Recommended: Hybrid Approach

**API Layer (Node.js)**
- **Runtime:** Node.js 20 LTS
- **Framework:** Express.js or Fastify
- **Validation:** Joi or Zod
- **Authentication:** JWT + API Keys
- **Rate Limiting:** express-rate-limit

**BOI Integration Layer (.NET)**
- **Runtime:** .NET 8.0 LTS
- **Framework:** ASP.NET Core Web API
- **COM Interop:** Native (System.Runtime.InteropServices)
- **DI Container:** Microsoft.Extensions.DependencyInjection

**Message Queue & Storage**
- **Primary:** Redis
- **Purpose:** Job queuing, status tracking, and temporary storage
- **Retention:** Completed jobs 7 days, failed jobs retained for review

**Monitoring & Logging**
- **Logs:** Serilog (.NET) / Winston (Node.js)
- **Storage:** File-based logging with volume mounts
- **Monitoring:** Health check endpoints, simple log review
- **Alerts:** Optional email notifications for critical errors

**Containerization**
- **Development & Production:** Docker Compose (sufficient for low volume)

---

## Project Structure

```
UAF-Auto/
├── README.md
├── docker-compose.yml              # Simplified stack: API + BOI + Redis
├── .env.example                    # Environment variables template
├── .gitignore
│
├── docs/
│   └── ARCHITECTURE.md             # This file
│
├── api/                            # Node.js API Service
│   ├── Dockerfile
│   ├── package.json
│   ├── src/
│   │   ├── server.js               # Entry point with graceful shutdown
│   │   ├── app.js                  # Express app configuration
│   │   ├── controllers/
│   │   │   ├── salesOrder.controller.js
│   │   │   └── health.controller.js
│   │   ├── services/
│   │   │   ├── validation.service.js
│   │   │   ├── queue.service.js    # Redis-based job tracking
│   │   │   ├── boiClient.service.js
│   │   │   └── processor.service.js # Simple background processor
│   │   ├── middleware/
│   │   │   ├── auth.middleware.js
│   │   │   └── errorHandler.middleware.js
│   │   ├── routes/
│   │   │   ├── salesOrders.routes.js
│   │   │   └── health.routes.js
│   │   ├── config/
│   │   │   └── index.js
│   │   └── utils/
│   │       └── logger.js
│   └── tests/
│       └── fixtures/
│           └── sample-order.json
│
├── sage-boi-service/               # .NET BOI Integration Service
│   ├── Dockerfile
│   ├── SageBOI.sln
│   └── SageBOI.Api/
│       ├── SageBOI.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Controllers/
│       │   ├── SalesOrderController.cs
│       │   └── HealthController.cs
│       ├── Services/
│       │   ├── IProvideXSessionManager.cs
│       │   ├── ProvideXSessionManager.cs
│       │   ├── ISalesOrderService.cs
│       │   └── SalesOrderService.cs
│       ├── Models/
│       │   ├── SalesOrderDTO.cs
│       │   ├── SalesOrderLineDTO.cs
│       │   ├── AddressDTO.cs
│       │   └── BOIResult.cs
│       └── Configuration/
│           └── SageConfiguration.cs
│
└── logs/                           # Application logs (volume mounted)
    ├── api/
    └── boi-service/
```

---

## Core Components

### 1. API Service (api/)

**Responsibilities:**
- Accept sales order requests from n8n
- Validate and sanitize input data
- Queue jobs for processing
- Return job status
- Provide health checks

**Key Features:**
- RESTful API design
- Async job submission (202 Accepted pattern)
- API key authentication
- Rate limiting (prevent abuse)
- Comprehensive error handling
- Request logging

**Technology:**
- Node.js 20 LTS
- Express.js (simple and reliable)
- Joi for validation
- ioredis for Redis connection
- Simple inline/background processing (no separate worker needed)

### 2. BOI Integration Service (sage-boi-service/)

**Responsibilities:**
- Manage ProvideX COM sessions
- Authenticate with Sage 100
- Create sales orders via BOI
- Handle BOI errors and retries
- Connection pooling

**Key Features:**
- Session pool (2-3 active sessions for low volume)
- Automatic session recovery
- Thread-safe operations
- Comprehensive BOI error mapping

**Technology:**
- .NET 8.0
- ASP.NET Core Web API
- Native COM Interop
- Dependency Injection

**Session Lifecycle:**
```
1. Initialize ProvideX.Script
2. Call pvx.Init(\\uaf-erp\Sage Premium 2022\MAS90\Home)
3. Create SY_Session object
4. Call session.nSetUser(username, password)
5. Call session.nSetCompany(companyCode)
6. Create SO_SalesOrder_bus object
7. Perform operations
8. Return session to pool (don't destroy)
```

### 3. Job Processing (Integrated in API)

**Processing Strategy:**
- **Synchronous:** Process immediately for urgent orders (return result directly)
- **Asynchronous:** Queue for background processing (return jobId, check status later)
- **Simple Background Worker:** setInterval-based processor checks queue every 10 seconds

**Retry Strategy:**
```
Attempt 1: Immediate
Attempt 2: Wait 30 seconds
Attempt 3: Wait 2 minutes
Failed: Mark as failed, log for manual review
```

### 4. Redis Storage

**Usage:**
```
Jobs stored as Redis hashes:
- Key: job:{jobId}
- Fields: status, data, result, error, createdAt, processedAt, attempts
- TTL: 7 days for completed jobs, no TTL for failed jobs

Queue implemented as Redis lists:
- sage:queue:normal (standard processing)
- sage:queue:high (priority processing)
```

**Job Data Structure:**
```json
{
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "queued|processing|completed|failed",
  "priority": "high|normal",
  "attempts": 0,
  "maxAttempts": 3,
  "createdAt": "2025-11-07T14:23:05Z",
  "processedAt": null,
  "data": {
    "sourceEmail": "customer@example.com",
    "orderDate": "2025-11-07",
    "customerNumber": "CUST001",
    "poNumber": "PO-12345",
    "items": [...],
    "shipToAddress": {...}
  },
  "result": null,
  "error": null
}
```

---

## Reliability Design

### Ensuring High Reliability (for Low Volume)

#### 1. Health Monitoring

**Health Check Endpoints:**
```
GET /health              # Basic liveness
GET /health/ready        # Readiness check (Redis + BOI service)
```

**Health Checks:**
- API service running?
- BOI service running?
- Can connect to Sage 100 server?
- Redis available?
- Queue depth reasonable?

#### 2. Graceful Degradation

**If Sage 100 is temporarily unavailable:**
- API continues accepting requests (returns 202 Accepted)
- Jobs queue in Redis
- Background processor pauses, retries when available
- When Sage 100 returns, processing resumes automatically
- **Zero data loss**

#### 3. Simple Deployment

**Deployment (uaf-erp server or workstation):**
```
Docker Compose with:
- API container (single instance)
- BOI service container (single instance)
- Redis (with RDB persistence)
```

**Why this is sufficient:**
- ~3-5 orders per day doesn't require load balancing
- Single instance can handle hundreds of orders per day
- Docker restart policies handle crashes
- Redis persistence prevents data loss on restart

#### 4. Data Persistence

**Job Retention:**
- Queue jobs: In Redis (RDB snapshots)
- Job history: In Redis (completed: 7 days, failed: indefinite)
- Logs: File-based with volume mount
- Audit trail: In Sage 100 (permanent)

**Backup Strategy:**
- Redis RDB snapshots automatically
- Application logs in mounted volume (easy to backup)
- Failed jobs retained in Redis for manual review

---

## Security

### 1. API Authentication

**API Key Authentication:**
```
X-API-Key: uaf_live_sk_abc123def456...

Keys stored as:
- Environment variable (production)
- Hashed in database
- Rotated every 90 days
```

**IP Whitelisting:**
```
Allowed IPs:
- n8n server IP
- Admin workstations
- Monitoring systems
```

### 2. Sage 100 Credentials

**Storage:**
```env
SAGE_USERNAME=api_user
SAGE_PASSWORD=encrypted_password
SAGE_COMPANY_DEV=MAS_TST
SAGE_COMPANY_PROD=MAS_UAF
SAGE_SERVER_PATH=\\uaf-erp\Sage Premium 2022\MAS90\Home
```

**Best Practices:**
- Use dedicated Sage user account (not personal)
- Minimum required permissions
- Never log passwords
- Rotate password every 90 days
- Store in environment variables (not config files)

### 3. Network Security

**Firewall Rules:**
```
Allow:
- n8n → API (port 443/HTTPS only)
- API → BOI Service (internal network)
- BOI Service → Sage 100 server (port 445/SMB)
- Workers → BOI Service (internal network)

Deny:
- Direct external access to BOI Service
- Direct external access to Redis
- Direct external access to PostgreSQL
```

### 4. Data Protection

**PII Handling:**
- Customer data encrypted in transit (TLS 1.3)
- Sensitive fields masked in logs
- No credit card data stored
- GDPR/compliance-ready logging

**Log Sanitization:**
```javascript
// Bad
logger.info(`Creating order for ${customer.email}, CC: ${customer.creditCard}`);

// Good
logger.info(`Creating order for ${hashEmail(customer.email)}, customer ID: ${customer.id}`);
```

---

## API Specification

### Base URL
```
Development: http://localhost:3000/api/v1
Production:  https://uaf-sage-api.yourdomain.com/api/v1
```

### Authentication
```
All requests require:
Header: X-API-Key: <your-api-key>
```

### Endpoints

#### 1. Create Sales Order

**Request:**
```http
POST /api/v1/sales-orders
Content-Type: application/json
X-API-Key: your-api-key-here

{
  "sourceEmail": "customer@example.com",
  "orderDate": "2025-11-07",
  "customerNumber": "CUST001",
  "poNumber": "PO-12345",
  "shipToAddress": {
    "name": "ABC Manufacturing",
    "address1": "123 Main St",
    "address2": "Suite 100",
    "city": "Phoenix",
    "state": "AZ",
    "zip": "85001",
    "country": "USA"
  },
  "items": [
    {
      "itemCode": "AIR-FILTER-20X25",
      "quantity": 50,
      "unitPrice": 12.99,
      "description": "20x25x1 Air Filter"
    }
  ],
  "notes": "Rush order - ship today if possible",
  "priority": "high"
}
```

**Response (Success):**
```http
HTTP/1.1 202 Accepted
Content-Type: application/json

{
  "success": true,
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "queued",
  "estimatedProcessingTime": "30 seconds",
  "message": "Sales order queued for processing",
  "_links": {
    "status": "/api/v1/sales-orders/job-abc123-2025-11-07-142305",
    "self": "/api/v1/sales-orders"
  }
}
```

**Response (Validation Error):**
```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request data",
    "details": [
      {
        "field": "items[0].quantity",
        "message": "Quantity must be greater than 0"
      }
    ]
  }
}
```

#### 2. Check Job Status

**Request:**
```http
GET /api/v1/sales-orders/job-abc123-2025-11-07-142305
X-API-Key: your-api-key-here
```

**Response (Completed):**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "success": true,
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "completed",
  "createdAt": "2025-11-07T14:23:05Z",
  "processedAt": "2025-11-07T14:23:35Z",
  "processingTime": "1.2s",
  "result": {
    "salesOrderNumber": "SO-00001234",
    "company": "MAS_TST",
    "customer": "CUST001",
    "totalAmount": 649.50,
    "sage100Response": {
      "returnCode": 0,
      "message": "Sales order created successfully"
    }
  }
}
```

**Response (Failed):**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "success": false,
  "jobId": "job-abc123-2025-11-07-142305",
  "status": "failed",
  "attempts": 5,
  "error": {
    "code": "CUSTOMER_NOT_FOUND",
    "message": "Customer CUST999 does not exist in Sage 100",
    "sage100Error": "<Error: 15 in Method NSETKEY>"
  }
}
```

#### 3. Health Checks

**Basic Health:**
```http
GET /health

HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "healthy",
  "timestamp": "2025-11-07T14:30:00Z",
  "uptime": "12h 35m 22s"
}
```

**Detailed Health:**
```http
GET /health/ready

HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "ready",
  "checks": {
    "api": "healthy",
    "boiService": "healthy",
    "redis": "healthy",
    "database": "healthy",
    "sage100": "healthy"
  },
  "queueDepth": 3,
  "activeJobs": 2
}
```

---

## Development Workflow

### Phase 1: Foundation (Week 1-2)

**Goals:**
- Set up development environment
- Create basic API structure
- Establish BOI connection
- Test sales order creation in MAS_TST

**Tasks:**
```
☐ Set up project repository
☐ Configure Docker Compose
☐ Create API service skeleton
☐ Implement health endpoints
☐ Create .NET BOI service
☐ Implement ProvideX session management
☐ Test BOI authentication
☐ Create test sales order via BOI
☐ Document BOI field mappings
```

**Success Criteria:**
- Can create a simple sales order in MAS_TST via API call
- Health endpoints return proper status
- Logs are structured and readable

### Phase 2: Queue & Reliability (Week 3-4)

**Goals:**
- Implement message queue
- Add retry logic
- Build worker process
- Test failure scenarios

**Tasks:**
```
☐ Set up Redis container
☐ Implement queue service in API
☐ Build worker process
☐ Add retry logic with exponential backoff
☐ Implement circuit breaker
☐ Create dead letter queue handler
☐ Test: Sage 100 unreachable scenario
☐ Test: Invalid customer scenario
☐ Test: Network timeout scenario
☐ Load testing (100 concurrent orders)
```

**Success Criteria:**
- Can process 100 orders in < 2 minutes
- Failed jobs retry automatically
- Dead letter queue captures persistent failures
- API stays responsive during Sage 100 outage

### Phase 3: n8n Integration (Week 5)

**Goals:**
- Design n8n workflow
- Implement PDF parsing
- Map PO data to Sage format
- End-to-end testing

**Tasks:**
```
☐ Set up n8n instance
☐ Configure email trigger (IMAP)
☐ Implement PDF extraction
☐ Build AI/OCR parser for PO data
☐ Create data mapping logic
☐ Implement validation rules
☐ Build n8n → API integration
☐ Test with real PO emails
☐ Handle edge cases (missing data, malformed PDFs)
☐ Create error notification workflow
```

**Success Criteria:**
- Can process real customer PO emails
- 95%+ accuracy on PO data extraction
- Invalid POs flagged for manual review
- Confirmation emails sent automatically

### Phase 4: Monitoring & Production (Week 6-7)

**Goals:**
- Set up comprehensive monitoring
- Configure alerting
- Load testing
- Production deployment
- Switch to MAS_UAF

**Tasks:**
```
☐ Set up Grafana dashboards
☐ Configure Prometheus metrics
☐ Implement structured logging
☐ Set up log aggregation (Seq/ELK)
☐ Configure alerts (email/Slack)
☐ Load test (500 orders/hour)
☐ Security audit
☐ Document deployment process
☐ Deploy to uaf-erp server
☐ Switch to MAS_UAF company
☐ Monitor for 1 week
☐ Train team on monitoring dashboard
```

**Success Criteria:**
- 99.9% uptime over 1 week
- All alerts trigger correctly
- Dashboard shows real-time metrics
- Team can troubleshoot using logs
- Production sales orders created successfully

---

## Deployment

### Development Environment

**Prerequisites:**
- Docker & Docker Compose installed
- Access to uaf-erp workstation
- Sage 100 user credentials (dyl account with SO permissions)
- .NET SDK 8.0 (for local BOI testing)

**Setup:**
```bash
# Clone repository
git clone <repo-url> UAF-Auto
cd UAF-Auto

# Copy environment template
cp .env.example .env

# Edit .env with your credentials
nano .env

# Start all services
docker-compose up -d

# Check logs
docker-compose logs -f api

# Test health endpoint
curl http://localhost:3000/health
```

**Environment Variables (.env):**
```env
# API Configuration
NODE_ENV=development
API_PORT=3000
API_KEY=dev_key_12345

# Sage 100 Configuration
SAGE_USERNAME=dyl
SAGE_PASSWORD=your_password_here
SAGE_COMPANY=MAS_TST
SAGE_SERVER_PATH=\\uaf-erp\Sage Premium 2022\MAS90\Home

# BOI Service Configuration
BOI_SERVICE_URL=http://sage-boi-service:5000
BOI_SESSION_POOL_SIZE=5
BOI_SESSION_TIMEOUT=300

# Redis Configuration
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=

# PostgreSQL Configuration
POSTGRES_HOST=postgres
POSTGRES_PORT=5432
POSTGRES_DB=uaf_sage_middleware
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres

# Queue Configuration
QUEUE_CONCURRENT_JOBS=5
QUEUE_MAX_RETRIES=5

# Logging
LOG_LEVEL=debug
```

### Production Deployment

**Server:** uaf-erp (or dedicated application server on same network)

**Deployment Steps:**

1. **Prepare Server**
```bash
# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Create application directory
sudo mkdir -p /opt/uaf-sage-middleware
cd /opt/uaf-sage-middleware
```

2. **Deploy Application**
```bash
# Clone repository
git clone <repo-url> .

# Create production .env
cp .env.example .env
nano .env  # Edit with production values

# Start services
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Verify all services running
docker-compose ps

# Check logs
docker-compose logs -f
```

3. **Configure Nginx (Reverse Proxy)**
```nginx
# /etc/nginx/sites-available/uaf-sage-api
server {
    listen 443 ssl http2;
    server_name uaf-sage-api.local;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Timeouts for long-running requests
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    location /health {
        proxy_pass http://localhost:3000/health;
        access_log off;
    }
}
```

4. **Set Up Monitoring**
```bash
# Start monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Access Grafana: http://server-ip:3001
# Default login: admin/admin
```

5. **Configure Alerts**
```yaml
# monitoring/prometheus/alerts.yml
groups:
  - name: sage_middleware_alerts
    rules:
      - alert: HighQueueDepth
        expr: sage_queue_depth > 100
        for: 5m
        annotations:
          summary: "Queue depth is high"
          
      - alert: HighErrorRate
        expr: rate(sage_errors_total[5m]) > 0.05
        for: 5m
        annotations:
          summary: "Error rate above 5%"
          
      - alert: SageUnreachable
        expr: sage_connectivity_status == 0
        for: 2m
        annotations:
          summary: "Cannot connect to Sage 100"
```

### Switching from MAS_TST to MAS_UAF

**When ready for production:**

1. **Update environment variable**
```bash
# Edit .env
SAGE_COMPANY=MAS_UAF  # Change from MAS_TST
```

2. **Verify permissions**
```bash
# Ensure dyl user has permissions in MAS_UAF
# Test with our BOI test script first
```

3. **Restart services**
```bash
docker-compose restart
```

4. **Monitor closely**
```bash
# Watch logs for 1 hour
docker-compose logs -f --tail=100

# Check dashboard
# http://server-ip:3001
```

5. **Create test order**
```bash
# Send test API request
curl -X POST http://server-ip:3000/api/v1/sales-orders \
  -H "X-API-Key: your-prod-key" \
  -H "Content-Type: application/json" \
  -d '{ ... test order data ... }'

# Verify in Sage 100 UI
```

---

## Monitoring

### Simple Logging & Monitoring

**Log Files:**
```
logs/api/
├── combined.log      # All API logs
├── error.log         # Errors only
└── access.log        # Request logs

logs/boi-service/
├── application.log   # All BOI service logs
└── errors.log        # Errors only
```

**What to Monitor:**
- Check logs daily for errors
- Failed jobs in Redis (manual review)
- Health endpoint status
- Disk space for log files

**Health Checks:**
```bash
# Check if services are running
curl http://localhost:3000/health
curl http://localhost:3000/health/ready

# Check Docker status
docker-compose ps

# View recent logs
docker-compose logs --tail=50 api
docker-compose logs --tail=50 sage-boi-service

# Check failed jobs in Redis
docker-compose exec redis redis-cli HGETALL "job:*" | grep "failed"
```

**Optional Alerts:**
- Set up simple email notification on critical errors
- Use cron job to check health endpoint and alert if down
- Log rotation to prevent disk filling

---

## Troubleshooting

### Common Issues

#### 1. BOI Error: NewObject Error 90

**Symptom:** Cannot create SO_SalesOrder_bus object

**Causes:**
- User lacks SO module permissions
- Company not properly set
- Module not licensed

**Solution:**
```
1. Verify user has SO permissions in Sage 100
2. Check company code is correct (MAS_TST or MAS_UAF)
3. Test with our BOI test script:
   cd /path/to/tests
   .\Sage100_BOI_Alternative.exe
4. Check Sage 100 module activation
```

#### 2. ProvideX Init Failed

**Symptom:** Error during pvx.Init()

**Causes:**
- Network path not accessible
- Config file missing
- ProvideX not registered

**Solution:**
```
1. Test network path:
   Test-Path "\\uaf-erp\Sage Premium 2022\MAS90\Home"
   
2. Verify config file:
   Test-Path "C:\Program Files (x86)\Common Files\Sage\Common Components\pvxcom.exe.config"
   
3. Check ProvideX registration:
   Test-Path "HKLM:\SOFTWARE\Classes\ProvideX.Script"
```

#### 3. Jobs Not Processing

**Symptom:** Jobs stuck in "queued" status

**Causes:**
- Sage 100 slow or unresponsive
- Background processor not running
- BOI service crashed

**Solution:**
```
1. Check BOI service health:
   curl http://localhost:5000/health
   
2. Check API logs for processor errors:
   docker-compose logs --tail=100 api
   
3. Restart API service (includes processor):
   docker-compose restart api
   
4. Check Sage 100 connectivity from BOI service
```

#### 4. Session Pool Exhausted

**Symptom:** "No available sessions" error

**Causes:**
- Too many concurrent requests
- Sessions not being released
- Session leak

**Solution:**
```
1. Check active sessions:
   curl http://localhost:5000/health/sessions
   
2. Increase pool size (temporary):
   Edit .env: BOI_SESSION_POOL_SIZE=10
   docker-compose restart sage-boi-service
   
3. Check for session leaks in logs
4. Restart BOI service to clear sessions:
   docker-compose restart sage-boi-service
```

#### 5. Invalid Customer Number

**Symptom:** Orders fail with "Customer not found"

**Causes:**
- Customer number not in Sage 100
- Wrong customer number format
- Customer in different company

**Solution:**
```
1. Verify customer exists in Sage 100 UI
2. Check customer number format (case sensitive)
3. Verify correct company is selected
4. Add customer validation to n8n workflow
```

### Logs Location

**Docker Logs:**
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api
docker-compose logs -f sage-boi-service
docker-compose logs -f worker

# Follow last 100 lines
docker-compose logs -f --tail=100 api
```

**Application Logs:**
```
logs/api/
├── combined.log
├── error.log
└── access.log

logs/boi-service/
├── application.log
└── errors.log
```

**Check Failed Jobs in Redis:**
```bash
# List all job keys
docker-compose exec redis redis-cli KEYS "job:*"

# Get job details
docker-compose exec redis redis-cli HGETALL "job:abc123"

# Count jobs by status
docker-compose exec redis redis-cli --scan --pattern "job:*" | \
  xargs -I {} redis-cli HGET {} status | sort | uniq -c
```

### Emergency Procedures

#### Complete System Restart

```bash
# Stop all services
docker-compose down

# Clear Redis (if corrupted)
docker volume rm uaf-auto_redis-data

# Restart
docker-compose up -d

# Verify
docker-compose ps
curl http://localhost:3000/health
```

#### Rollback Deployment

```bash
# Stop current version
docker-compose down

# Checkout previous version
git checkout <previous-commit-hash>

# Redeploy
docker-compose up -d
```

#### Manual Job Processing

```javascript
// If queue is stuck, process jobs manually via API
const Redis = require('ioredis');
const redis = new Redis();

const job = await redis.lpop('sage-sales-orders:priority:normal');
const jobData = JSON.parse(job);

// Call BOI service directly
const response = await fetch('http://localhost:5000/api/sales-orders', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(jobData.data)
});
```

---

## Contact & Support

**Project Lead:** Dylan Buck  
**Sage 100 Admin:** [Name]  
**IT Contact:** [Name]

**Resources:**
- Sage 100 Documentation: https://help.sagecloud.com/sage100/
- Sage Community: https://communityhub.sage.com/us/sage100/
- n8n Documentation: https://docs.n8n.io/

**Change Log:**
- 2025-11-07: Initial architecture documented
- [Date]: [Change description]

---

**Last Updated:** November 7, 2025  
**Version:** 1.0  
**Status:** Development Phase

