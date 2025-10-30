# IBM MQ Integration Testing Guide

## Overview

This guide explains how to run IBM MQ integration tests for the CardActions microservice.

## Prerequisites

- Docker Desktop installed and running
- .NET 8 SDK installed
- Docker Compose v1.29+ or Docker Compose V2

## Test Scripts Available

### 1. run-ibmmq-tests.sh (Recommended)

**Full-featured test runner with Docker orchestration**

**Features:**
- Automatically starts IBM MQ container
- Waits for IBM MQ health check (90s timeout)
- Sets environment variables
- Runs integration tests
- Optional cleanup after tests
- Colored output and progress indicators
- Error handling and logging

**Usage:**
```bash
# Run tests and leave IBM MQ running
./run-ibmmq-tests.sh

# Run tests and stop IBM MQ after completion
./run-ibmmq-tests.sh --cleanup

# Explicitly keep IBM MQ running after tests
./run-ibmmq-tests.sh --no-cleanup
```

**Output Example:**
```
========================================
IBM MQ Integration Test Runner
========================================

[1/6] Checking prerequisites...
✓ Prerequisites verified

[2/6] Starting IBM MQ container...
✓ IBM MQ container started

[3/6] Waiting for IBM MQ to initialize (up to 90s)...
IBM MQ requires 60-90 seconds for full initialization
....................
✓ IBM MQ is ready

[4/6] Setting environment variables...
IBMMQ_QUEUE_MANAGER=QM1
IBMMQ_HOST=localhost
IBMMQ_PORT=1414
✓ Environment configured

[5/6] Running IBM MQ integration tests...
[Test output]
✓ All IBM MQ integration tests passed

[6/6] Cleanup...
IBM MQ container is still running

========================================
Test Execution Complete
========================================
```

---

### 2. run-ibmmq-tests-simple.sh

**Lightweight test runner (assumes IBM MQ is already running)**

**Features:**
- Sets environment variables only
- Runs tests immediately
- No Docker orchestration
- Minimal output

**Usage:**
```bash
# First, start IBM MQ manually:
docker-compose --profile ibmmq up -d

# Then run tests:
./run-ibmmq-tests-simple.sh

# Stop IBM MQ when done:
docker-compose --profile ibmmq down
```

**When to use:**
- IBM MQ is already running
- Running tests multiple times
- Debugging test failures
- Faster test iterations

---

## Manual Test Execution

### Step-by-Step Process

#### 1. Start IBM MQ Container
```bash
docker-compose --profile ibmmq up -d
```

#### 2. Wait for IBM MQ Initialization
IBM MQ requires 60-90 seconds to fully initialize. Check status:
```bash
# Check container health
docker ps --filter "name=cardactions-ibmmq"

# View logs
docker logs cardactions-ibmmq

# Wait for "AMQ8004I: IBM MQ Queue Manager" message
docker logs -f cardactions-ibmmq | grep "AMQ8004I"
```

#### 3. Set Environment Variables
```bash
export IBMMQ_QUEUE_MANAGER=QM1
export IBMMQ_HOST=localhost
export IBMMQ_PORT=1414
export IBMMQ_CHANNEL=DEV.APP.SVRCONN
export IBMMQ_QUEUE_NAME=DEV.QUEUE.1
export IBMMQ_USERNAME=app
export IBMMQ_PASSWORD=passw0rd
```

#### 4. Run Tests
```bash
# Run all IBM MQ integration tests
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=IbmMQ"

# Run with verbose output
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=IbmMQ" \
    --logger "console;verbosity=detailed"

# Run specific test
dotnet test tests/CardActions.IntegrationTests \
    --filter "FullyQualifiedName~IbmMqEventBusIntegrationTests.PublishAsync_WithValidEvent_ShouldPublishSuccessfully"
```

#### 5. Stop IBM MQ (when done)
```bash
docker-compose --profile ibmmq down
```

---

## Test Configuration

### Configuration Priority (Highest to Lowest)

1. **Environment Variables** (set via export or scripts)
   - `IBMMQ_QUEUE_MANAGER`
   - `IBMMQ_HOST`
   - `IBMMQ_PORT`
   - `IBMMQ_CHANNEL`
   - `IBMMQ_QUEUE_NAME`
   - `IBMMQ_USERNAME`
   - `IBMMQ_PASSWORD`

2. **appsettings.Test.json** (in `tests/CardActions.IntegrationTests/`)
   ```json
   {
     "EventBus": {
       "IbmMQ": {
         "QueueManager": "QM1",
         "Host": "localhost",
         "Port": 1414,
         "Channel": "DEV.APP.SVRCONN",
         "QueueName": "DEV.QUEUE.1",
         "Username": "app",
         "Password": "passw0rd"
       }
     }
   }
   ```

3. **Default Values** (hardcoded in test constructor)

---

## Troubleshooting

### IBM MQ Container Won't Start

**Symptoms:**
```
Error: Failed to start IBM MQ container
```

**Solutions:**
1. Check if port 1414 is already in use:
   ```bash
   lsof -i :1414  # macOS/Linux
   netstat -ano | findstr :1414  # Windows
   ```

2. Check Docker logs:
   ```bash
   docker logs cardactions-ibmmq
   ```

3. Ensure Docker has enough resources (4GB RAM minimum)

4. Clean up and retry:
   ```bash
   docker-compose --profile ibmmq down -v
   docker-compose --profile ibmmq up -d
   ```

---

### IBM MQ Health Check Timeout

**Symptoms:**
```
Error: IBM MQ did not become healthy within 90s
```

**Solutions:**
1. IBM MQ requires 60-90 seconds on first startup
2. Wait longer and check logs:
   ```bash
   docker logs -f cardactions-ibmmq
   ```
3. Look for "AMQ8004I: IBM MQ Queue Manager 'QM1' started" message
4. Increase timeout in script if needed

---

### Connection Refused During Tests

**Symptoms:**
```
IBM MQ error - ReasonCode: 2059 (MQRC_Q_MGR_NOT_AVAILABLE)
```

**Solutions:**
1. Verify IBM MQ is fully initialized:
   ```bash
   docker exec cardactions-ibmmq dspmq
   ```
   Expected output: `QMNAME(QM1) STATUS(Running)`

2. Verify queue exists:
   ```bash
   docker exec cardactions-ibmmq runmqsc QM1 <<< "DISPLAY QUEUE(DEV.QUEUE.1)"
   ```

3. Check channel status:
   ```bash
   docker exec cardactions-ibmmq runmqsc QM1 <<< "DISPLAY CHANNEL(DEV.APP.SVRCONN)"
   ```

---

### Tests Pass But Events Not Published

**Symptoms:**
- Tests pass with no errors
- Logger shows "Event published" messages
- But queue appears empty

**Explanation:**
This is expected behavior. Integration tests verify:
1. Connection establishment
2. Message serialization
3. MQ API calls succeed
4. No exceptions thrown

The tests do NOT verify message consumption (no consumer implemented).

To verify messages were actually published:
```bash
docker exec cardactions-ibmmq runmqsc QM1 <<< "DISPLAY QSTATUS(DEV.QUEUE.1) CURDEPTH"
```

---

## IBM MQ Console Access

**URL:** https://localhost:9443/ibmmq/console

**Credentials:**
- Username: `admin`
- Password: `passw0rd`

**Note:** Accept the self-signed certificate warning in your browser.

---

## Continuous Integration

### GitHub Actions Example

```yaml
name: IBM MQ Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      ibmmq:
        image: ibmcom/mq:latest
        ports:
          - 1414:1414
          - 9443:9443
        env:
          LICENSE: accept
          MQ_QMGR_NAME: QM1
          MQ_APP_PASSWORD: passw0rd
        options: >-
          --health-cmd "chkmqhealthy"
          --health-interval 30s
          --health-timeout 10s
          --health-retries 5
          --health-start-period 60s
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Run IBM MQ Integration Tests
        run: ./run-ibmmq-tests-simple.sh
        env:
          IBMMQ_HOST: localhost
          IBMMQ_PORT: 1414
```

---

## Cleanup

### Remove IBM MQ Container and Data
```bash
# Stop and remove container
docker-compose --profile ibmmq down

# Remove volume data (WARNING: deletes all queued messages)
docker-compose --profile ibmmq down -v
```

### Remove IBM MQ Image
```bash
docker rmi ibmcom/mq:latest
```

---

## Additional Resources

- [IBM MQ Documentation](https://www.ibm.com/docs/en/ibm-mq)
- [IBM MQ Docker Hub](https://hub.docker.com/r/ibmcom/mq)
- [IBM MQ Developer Edition](https://developer.ibm.com/components/ibm-mq/)

---

## Summary

**Quick Start:**
```bash
# Automated (recommended)
./run-ibmmq-tests.sh

# Manual
docker-compose --profile ibmmq up -d
# Wait 90 seconds
./run-ibmmq-tests-simple.sh
docker-compose --profile ibmmq down
```

**Default Configuration:**
- Queue Manager: QM1
- Host: localhost
- Port: 1414
- Channel: DEV.APP.SVRCONN
- Queue: DEV.QUEUE.1
- Username: app
- Password: passw0rd