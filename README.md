# Card Actions Microservice

[![.NET](https://github.com/your-org/CardActions/actions/workflows/dotnet.yml/badge.svg)](https://github.com/your-org/CardActions/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET 8 microservice for determining allowed card actions based on card type, status, and PIN configuration.

## Table of Contents
- [Features](#features)
- [Running the Solution](#running-the-solution)
  - [Local Development](#local-development)
  - [Docker Development](#docker-development)
  - [Event Bus Configuration](#event-bus-configuration)
    - [InMemory Event Bus](#inmemory-event-bus)
    - [RabbitMQ Integration](#rabbitmq-integration)
    - [IBM MQ Integration](#ibm-mq-integration)
- [Testing](#testing)
  - [Unit Tests](#unit-tests)
  - [Integration Tests](#integration-tests)
  - [Event Bus Testing](#event-bus-testing)
- [API Documentation](#api-documentation)
- [Architecture](#architecture)
- [Deployment](#deployment)
- [License](#license)

## Features

- REST API with OpenAPI documentation
- Clean Architecture with Domain-Driven Design
- Comprehensive error handling with correlation IDs
- Unit and integration tests (100% coverage)
- Docker containerization support
- Health check endpoints with provider-specific monitoring
- OpenTelemetry integration (metrics & distributed tracing)
- **Multi-Provider Event Bus Support:**
  - InMemory (local development/testing)
  - RabbitMQ (production-ready message broker)
  - IBM MQ (enterprise message queue)
- Feature toggle for switching event bus providers
- Resilient event publishing with automatic retry

## Running the Solution

### Local Development

#### Prerequisites
- .NET 8 SDK
- Docker Desktop (optional, for RabbitMQ/IBM MQ)
- IDE: Visual Studio 2022, VS Code, or Rider

#### Basic Setup
```bash
# Clone repository
git clone https://github.com/your-org/CardActions.git
cd CardActions

# Restore dependencies
dotnet restore

# Run the application (uses InMemory event bus by default)
dotnet run --project src/CardActions.API
```

Application runs at:
- HTTPS: https://localhost:49510
- HTTP: http://localhost:49511

#### Environment Configuration
The service uses these environment variables:
- `ASPNETCORE_ENVIRONMENT`: Development | Production
- `EventBus__Provider`: InMemory | RabbitMQ | IbmMQ
- Additional provider-specific configuration (see sections below)

### Docker Development

#### Running with Docker Compose
```bash
# Build and start API only (InMemory event bus)
docker-compose up --build

# Or run in detached mode:
docker-compose up -d --build
```

Application runs at:
- HTTPS: https://localhost:49510
- HTTP: http://localhost:49511
- Swagger: https://localhost:49510/swagger
- Health: https://localhost:49510/health

#### Docker Compose Profiles
The solution supports profile-based service startup:

- **Default**: Runs only the API with InMemory event bus
- **RabbitMQ**: Include `--profile rabbitmq` to start RabbitMQ container
- **IBM MQ**: Include `--profile ibmmq` to start IBM MQ container

```bash
# Run with RabbitMQ
docker-compose --profile rabbitmq up -d

# Run with IBM MQ
docker-compose --profile ibmmq up -d

# Run both message brokers (not recommended for production)
docker-compose --profile rabbitmq --profile ibmmq up -d
```

#### Building Docker Image
```bash
# Build the Docker image
docker build -t cardactions-api -f src/CardActions.API/Dockerfile .

# Run container
docker run -p 8080:80 -p 8443:443 cardactions-api
```

---

## Event Bus Configuration

The service supports three event bus providers with seamless switching via configuration.

### InMemory Event Bus

**Use Case:** Local development, testing, demo environments

**Configuration:**
```json
{
  "EventBus": {
    "Provider": "InMemory"
  }
}
```

**Features:**
- No external dependencies
- Synchronous event handling
- Zero latency
- Perfect for unit tests

**Startup:**
```bash
# Already configured by default in appsettings.Development.json
dotnet run --project src/CardActions.API
```

---

### RabbitMQ Integration

**Use Case:** Production environments requiring reliable message delivery

**Prerequisites:**
- RabbitMQ 3.12+ running (via Docker or standalone)

**Configuration:**
```json
{
  "EventBus": {
    "Provider": "RabbitMQ",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "cardactions",
      "Password": "devpassword",
      "VirtualHost": "/",
      "Exchange": "cardactions.events"
    }
  }
}
```

**Quick Start:**

1. **Start RabbitMQ container:**
   ```bash
   docker-compose --profile rabbitmq up -d
   ```

2. **Update configuration:**
   ```bash
   export EventBus__Provider=RabbitMQ
   ```

3. **Run application:**
   ```bash
   dotnet run --project src/CardActions.API
   ```

**Access RabbitMQ Management UI:**
- URL: http://localhost:15672
- Username: `cardactions`
- Password: `devpassword`

**Health Check:**
- Endpoint: https://localhost:49510/health
- RabbitMQ status included in health report

**Stop RabbitMQ:**
```bash
docker-compose --profile rabbitmq down
```

---

### IBM MQ Integration

**Use Case:** Enterprise environments with existing IBM MQ infrastructure

**Prerequisites:**
- IBM MQ 9.3+ running (via Docker or standalone)
- Queue Manager configured with required queues

**Configuration:**
```json
{
  "EventBus": {
    "Provider": "IbmMQ",
    "IbmMQ": {
      "QueueManager": "QM1",
      "Host": "localhost",
      "Port": 1414,
      "Channel": "DEV.APP.SVRCONN",
      "QueueName": "DEV.QUEUE.1",
      "Username": "app",
      "Password": "passw0rd",
      "ConnectionTimeoutSeconds": 30,
      "UseSsl": false
    }
  }
}
```

**Quick Start:**

1. **Start IBM MQ container:**
   ```bash
   docker-compose --profile ibmmq up -d
   ```
   
   Note: IBM MQ requires 60-90 seconds for full initialization

2. **Wait for IBM MQ to be ready:**
   ```bash
   # Check logs for startup completion
   docker logs -f cardactions-ibmmq
   
   # Look for: "AMQ8004I: IBM MQ Queue Manager 'QM1' started"
   ```

3. **Update configuration:**
   ```bash
   export EventBus__Provider=IbmMQ
   ```

4. **Run application:**
   ```bash
   dotnet run --project src/CardActions.API
   ```

**Access IBM MQ Console:**
- URL: https://localhost:9443/ibmmq/console
- Username: `admin`
- Password: `passw0rd`
- Note: Accept self-signed certificate warning

**Health Check:**
- Endpoint: https://localhost:49510/health
- IBM MQ status included (Queue Manager connection + queue depth)

**Stop IBM MQ:**
```bash
docker-compose --profile ibmmq down
```

**Additional Documentation:**
- See [IBM_MQ_TESTING_GUIDE.md](IBM_MQ_TESTING_GUIDE.md) for comprehensive setup and troubleshooting

---

## Testing

### Unit Tests
```bash
# Run all unit tests
dotnet test tests/CardActions.UnitTests

# Run with coverage
dotnet test tests/CardActions.UnitTests \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover

# Run specific test category
dotnet test tests/CardActions.UnitTests \
    --filter "Category=Unit"

# Run specific feature tests
dotnet test tests/CardActions.UnitTests \
    --filter "Feature=IbmMqEventBus"
```

**Test Structure:**
- Domain logic tests (CardActionPolicy)
- Event bus implementation tests (InMemory, RabbitMQ, IBM MQ)
- Configuration validation tests
- Health check tests

---

### Integration Tests
```bash
# Run all integration tests
dotnet test tests/CardActions.IntegrationTests

# Run API integration tests only
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Feature=CardActions"
```

---

### Event Bus Testing

#### InMemory Event Bus
```bash
# Included in unit tests (no external dependencies)
dotnet test tests/CardActions.UnitTests \
    --filter "Feature=InMemoryEventBus"
```

#### RabbitMQ Integration Tests

**Option 1: Automated Test Runner (Recommended)**
```bash
# Starts RabbitMQ, runs tests, optionally cleans up
./run-rabbitmq-tests.sh

# With cleanup after tests
./run-rabbitmq-tests.sh --cleanup
```

**Option 2: Manual Execution**
```bash
# 1. Start RabbitMQ
docker-compose --profile rabbitmq up -d

# 2. Run tests
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=RabbitMQ"

# 3. Stop RabbitMQ
docker-compose --profile rabbitmq down
```

#### IBM MQ Integration Tests

**Option 1: Automated Test Runner (Recommended)**
```bash
# Full automation with health check waiting
./run-ibmmq-tests.sh

# With automatic cleanup after tests
./run-ibmmq-tests.sh --cleanup
```

**Option 2: Manual Execution**
```bash
# 1. Start IBM MQ
docker-compose --profile ibmmq up -d

# 2. Wait for IBM MQ initialization (60-90 seconds)
docker logs -f cardactions-ibmmq | grep "AMQ8004I"

# 3. Run tests
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=IbmMQ"

# 4. Stop IBM MQ
docker-compose --profile ibmmq down
```

**Option 3: Simple Test Runner**
```bash
# Assumes IBM MQ is already running
./run-ibmmq-tests-simple.sh
```

**Test Coverage:**
- Connection establishment and validation
- Single event publishing
- Batch event publishing
- Error handling and retry logic
- Connection failure scenarios

---

## API Documentation

### Base URLs
- Local: https://localhost:49510/api/v1
- Docker: https://localhost:49510/api/v1

### Endpoints

#### Get Card Actions
```http
GET /api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}
```

**Response:**
```json
{
  "cardNumber": "CREDIT_ACTIVE",
  "allowedActions": ["ACTION1", "ACTION2", "ACTION5"],
  "traceId": "abc123"
}
```

### Swagger UI
- Interactive API: https://localhost:49510/swagger
- OpenAPI Spec: https://localhost:49510/swagger/v1/swagger.json

### Health Checks
```http
GET /health
```

**Response includes:**
- API health status
- Event bus provider status
- Message broker connectivity (if RabbitMQ/IBM MQ)
- Queue/Exchange status

**Example Response:**
```json
{
  "status": "Healthy",
  "results": {
    "self": {
      "status": "Healthy"
    },
    "ibmmq": {
      "status": "Healthy",
      "data": {
        "queueManager": "QM1",
        "host": "localhost:1414",
        "queue": "DEV.QUEUE.1",
        "queueDepth": 0,
        "connected": true
      }
    }
  }
}
```

---

## Architecture

### Clean Architecture Layers

The solution follows Clean Architecture principles with clear separation of concerns:

```
CardActions/
├── src/
│   ├── CardActions.API/              # Presentation Layer
│   │   ├── Controllers/              # REST API endpoints
│   │   ├── Middleware/               # Error handling, logging
│   │   └── BackgroundServices/       # API health tests
│   │
│   ├── CardActions.Application/      # Application Layer
│   │   ├── DTOs/                     # Data Transfer Objects
│   │   ├── Services/                 # Application services
│   │   └── Interfaces/               # Repository contracts
│   │
│   ├── CardActions.Domain/           # Domain Layer (Core)
│   │   ├── Models/                   # Domain entities
│   │   ├── Enums/                    # Domain enumerations
│   │   ├── Events/                   # Domain events
│   │   ├── Services/                 # Domain services & policies
│   │   └── Abstractions/             # Core interfaces (IEventBus)
│   │
│   └── CardActions.Infrastructure/   # Infrastructure Layer
│       ├── Data/                     # Data access (Repository)
│       ├── EventBus/                 # Event bus implementations
│       │   ├── InMemoryEventBus.cs
│       │   ├── RabbitMQEventBus.cs
│       │   ├── IbmMqEventBus.cs
│       │   └── ResilientEventBus.cs  # Decorator with retry
│       ├── HealthChecks/             # Custom health checks
│       │   ├── RabbitMQHealthCheck.cs
│       │   └── IbmMqHealthCheck.cs
│       └── Configuration/            # Configuration models
│
├── tests/
│   ├── CardActions.UnitTests/        # Unit tests (40+ tests)
│   └── CardActions.IntegrationTests/ # Integration tests
│
├── run-rabbitmq-tests.sh             # RabbitMQ test automation
├── run-ibmmq-tests.sh                # IBM MQ test automation (full)
├── run-ibmmq-tests-simple.sh         # IBM MQ test automation (simple)
├── docker-compose.yml                # Container orchestration
└── docker-compose.override.yml       # Local development overrides
```

### Event-Driven Architecture

**Domain Events:**
- `CardActionsRetrievedEvent` - Published when card actions are successfully retrieved
- `CardNotFoundEvent` - Published when card lookup fails

**Event Flow:**
1. API receives request → CardActionsService processes
2. Repository retrieves card data
3. CardActionPolicy evaluates allowed actions
4. Domain event published to configured event bus
5. Event serialized and sent to message broker (if configured)

**Resilient Publishing:**
All event bus implementations are wrapped with `ResilientEventBus` decorator providing:
- Automatic retry with exponential backoff
- Circuit breaker pattern
- Comprehensive logging

---

## Deployment

### Deployment Targets

The service is production-ready and supports multiple deployment platforms:

- **Docker Containers** (recommended)
- **Kubernetes** (with Helm charts)
- **Azure App Service**
- **AWS ECS/Fargate**
- **On-premises** (with IBM MQ integration)

### Configuration Management

**Development:**
- `appsettings.Development.json` - InMemory event bus
- `docker-compose.override.yml` - Local Docker overrides

**Production:**
- `appsettings.Production.json` - Provider-specific configuration
- Environment variables (12-factor app compliant)
- Azure Key Vault / AWS Secrets Manager for credentials

**Environment Variable Examples:**
```bash
# Event Bus Provider Selection
export EventBus__Provider=IbmMQ

# IBM MQ Configuration
export EventBus__IbmMQ__QueueManager=PROD_QM1
export EventBus__IbmMQ__Host=ibmmq.production.local
export EventBus__IbmMQ__Port=1414
export EventBus__IbmMQ__Channel=PROD.APP.SVRCONN
export EventBus__IbmMQ__QueueName=PROD.CARDACTIONS.EVENTS
export EventBus__IbmMQ__Username=app_user
export EventBus__IbmMQ__Password=${IBM_MQ_PASSWORD}  # From secrets vault
export EventBus__IbmMQ__UseSsl=true
```

### Health Monitoring

**Liveness Probe:**
```http
GET /health
```

**Readiness Probe:**
```http
GET /health/ready
```

**Kubernetes Example:**
```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 80
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health/ready
    port: 80
  initialDelaySeconds: 5
  periodSeconds: 10
```

---

## Event Bus Provider Comparison

| Feature | InMemory | RabbitMQ | IBM MQ |
|---------|----------|----------|--------|
| **Use Case** | Development/Testing | Cloud/Microservices | Enterprise Integration |
| **External Dependency** | None | RabbitMQ Server | IBM MQ Queue Manager |
| **Async Processing** | No | Yes | Yes |
| **Message Persistence** | No | Yes | Yes |
| **Transaction Support** | N/A | No | Yes |
| **Performance** | Fastest | High | High |
| **Scalability** | Single instance | High (clustering) | High (clustering) |
| **Monitoring** | No | Management UI | Console + CLI |
| **Setup Complexity** | None | Low | Medium |
| **Cost** | Free | Free (OSS) | Licensed |

---

## Performance Characteristics

**API Response Time:** <100ms (p95)
**Event Publishing:** <10ms (InMemory), <50ms (RabbitMQ/IBM MQ)
**Throughput:** 1000+ req/s (single instance)
**Memory:** ~50MB baseline

---

## Contributing

1. Fork the repository
2. Create a feature branch
3. Follow Clean Architecture principles
4. Add unit tests (maintain 100% coverage)
5. Update documentation
6. Submit pull request

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

---

## Additional Resources

- [IBM MQ Testing Guide](IBM_MQ_TESTING_GUIDE.md) - Comprehensive testing documentation
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design](https://martinfowler.com/bliki/DomainDrivenDesign.html)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [IBM MQ Documentation](https://www.ibm.com/docs/en/ibm-mq)

---

Built with .NET 8 and Clean Architecture principles