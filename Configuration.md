# Configuration Guide – Event Bus Integration

## 🔧 Environment Variables (.env)

| Variable | Description |
|----------|-------------|
| `USE_IN_MEMORY_EVENT_BUS` | Toggle between InMemory and RabbitMQ |
| `RABBITMQ_HOST` | RabbitMQ hostname |
| `RABBITMQ_PORT` | RabbitMQ port (default: 5672) |
| `RABBITMQ_USERNAME` | RabbitMQ user |
| `RABBITMQ_PASSWORD` | RabbitMQ password |
| `RABBITMQ_EXCHANGE` | Exchange name (e.g., `cardactions.events`) |
| `RABBITMQ_VHOST` | Virtual host (e.g., `/production`) |

## 🧱 Configuration Binding

- Bound in `Program.cs` via `builder.Configuration.GetSection("EventBus")`
- Injected into `RabbitMQEventBus` via `IOptions<EventBusOptions>`

## 📦 File Locations

| File | Purpose |
|------|---------|
| `.env` | Local environment variables |
| `docker-compose.yml` | Container environment injection |
| `EventBusOptions.cs` | Strongly-typed configuration |
| `Program.cs` | Configuration binding and DI toggle |

## ✅ Best Practices

- Use `.env` for local/dev; secrets manager for production
- Keep RabbitMQ credentials out of source control
- Validate configuration on startup (optional health check)
