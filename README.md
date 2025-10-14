# Card Actions Microservice v2

[![.NET](https://github.com/your-org/CardActions/actions/workflows/dotnet.yml/badge.svg)](https://github.com/your-org/CardActions/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET 8 microservice for determining allowed card actions based on card type, status, and PIN configuration.

## Table of Contents
- [Features](#features)
- [Running the Solution](#running-the-solution)
  - [Local Development](#local-development)
  - [Docker Development](#docker-development)
  - [RabbitMQ Integration](#rabbitmq-integration)
- [Testing](#testing)
  - [Unit Tests](#unit-tests)
  - [Integration Tests](#integration-tests)
  - [RabbitMQ Tests](#rabbitmq-tests)
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
- Health check endpoint
- OpenTelemetry integration
- RabbitMQ event bus integration
- In-memory event bus for local development

## Running the Solution

### Local Development

#### Prerequisites
- .NET 8 SDK
- Docker Desktop (optional, for RabbitMQ)
- IDE: Visual Studio 2022, VS Code, or Rider

#### Basic Setup
```bash
# Clone repository
git clone https://github.com/your-org/CardActions.git
cd CardActions

# Restore dependencies
dotnet restore

# Run the application
dotnet run --project src/CardActions.API
```

Application runs at:
- HTTPS: https://localhost:49510
- HTTP: http://localhost:49511

#### Environment Configuration
The service uses these environment variables:
- `ASPNETCORE_ENVIRONMENT`: Set to "Development" for local dev
- `EventBus__UseInMemory`: Set to "true" for in-memory bus (default in override)
- For RabbitMQ configuration, see the Docker section below

### Docker Development

#### Running with Docker Compose
```bash
# Build and start all services (API + RabbitMQ)
docker-compose up --build

# Or to run in detached mode:
docker-compose up -d --build
```

Application runs at:
- HTTPS: https://localhost:49510
- HTTP: http://localhost:49511
- RabbitMQ Management: http://localhost:15672 (username: cardactions, password: devpassword)

#### Docker Compose Profiles
- Default: Runs only the API with in-memory event bus
- RabbitMQ: Include `--profile rabbitmq` to start RabbitMQ container

#### Building Docker Image
```bash
# Build the Docker image
docker build -t cardactions-api .

# Run container
docker run -p 8080:80 -p 8443:443 cardactions-api
```

### RabbitMQ Integration

To enable RabbitMQ event bus:
1. Start RabbitMQ container: `docker-compose --profile rabbitmq up -d rabbitmq`
2. Set environment variable: `EventBus__UseInMemory=false`
3. The API will automatically connect to RabbitMQ using credentials from docker-compose.yml

## Testing

### Unit Tests
```bash
# Run all unit tests
dotnet test tests/CardActions.UnitTests

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test category
dotnet test --filter "Category=ACTION8"
```

### Integration Tests
```bash
# Run all integration tests
dotnet test tests/CardActions.IntegrationTests
```

### RabbitMQ Tests
```bash
# Run RabbitMQ-specific tests
./run-rabbitmq-tests.sh
```

## API Documentation

### Base URLs
- Local: https://localhost:49510/api/v1
- Docker: https://localhost:49510/api/v1

### Swagger UI
- https://localhost:49510/swagger
- OpenAPI Spec: https://localhost:49510/swagger/v1/swagger.json

## Architecture

### Project Structure
```
CardActions/
├── src/
│   ├── CardActions.API/              # Presentation Layer
│   ├── CardActions.Application/      # Application Layer
│   ├── CardActions.Domain/           # Domain Layer
│   └── CardActions.Infrastructure/   # Infrastructure Layer
├── tests/
│   ├── CardActions.UnitTests/        # Unit tests
│   └── CardActions.IntegrationTests/ # Integration tests
├── Dockerfile                        # Multi-stage Docker build
├── docker-compose.yml                # Container orchestration
└── docker-compose.override.yml       # Local development overrides
```

## Deployment

The service is ready for deployment with:
- Docker containers
- Kubernetes
- Azure App Service
- AWS ECS

## License

MIT License - see [LICENSE](LICENSE) file for details.

Built with ❤️ using .NET 8 and Clean Architecture principles