#!/bin/bash

# run-ibmmq-tests.sh
# Integration test runner for IBM MQ event bus
# Starts IBM MQ container, runs tests, and optionally cleans up

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
IBM_MQ_CONTAINER="cardactions-ibmmq"
IBM_MQ_STARTUP_TIMEOUT=90  # IBM MQ takes 60-90s to fully initialize
CLEANUP_AFTER_TESTS=false  # Set to true to auto-cleanup

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --cleanup)
            CLEANUP_AFTER_TESTS=true
            shift
            ;;
        --no-cleanup)
            CLEANUP_AFTER_TESTS=false
            shift
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Usage: $0 [--cleanup|--no-cleanup]"
            exit 1
            ;;
    esac
done

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}IBM MQ Integration Test Runner${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}[1/6] Checking prerequisites...${NC}"
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed or not in PATH${NC}"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo -e "${RED}Error: docker-compose is not installed or not in PATH${NC}"
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK is not installed or not in PATH${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Prerequisites verified${NC}"
echo ""

# Start IBM MQ container
echo -e "${YELLOW}[2/6] Starting IBM MQ container...${NC}"
docker-compose --profile ibmmq up -d ibmmq

if [ $? -ne 0 ]; then
    echo -e "${RED}Error: Failed to start IBM MQ container${NC}"
    exit 1
fi

echo -e "${GREEN}✓ IBM MQ container started${NC}"
echo ""

# Wait for IBM MQ to be ready
echo -e "${YELLOW}[3/6] Waiting for IBM MQ to initialize (up to ${IBM_MQ_STARTUP_TIMEOUT}s)...${NC}"
echo "IBM MQ requires 60-90 seconds for full initialization"

ELAPSED=0
READY=false

while [ $ELAPSED -lt $IBM_MQ_STARTUP_TIMEOUT ]; do
    # Check if container is running
    if ! docker ps --filter "name=$IBM_MQ_CONTAINER" --filter "status=running" | grep -q $IBM_MQ_CONTAINER; then
        echo -e "${RED}Error: IBM MQ container is not running${NC}"
        docker logs $IBM_MQ_CONTAINER
        exit 1
    fi
    
    # Check health status using docker inspect
    HEALTH_STATUS=$(docker inspect --format='{{.State.Health.Status}}' $IBM_MQ_CONTAINER 2>/dev/null || echo "unknown")
    
    if [ "$HEALTH_STATUS" = "healthy" ]; then
        READY=true
        break
    fi
    
    echo -n "."
    sleep 5
    ELAPSED=$((ELAPSED + 5))
done

echo ""

if [ "$READY" = false ]; then
    echo -e "${RED}Error: IBM MQ did not become healthy within ${IBM_MQ_STARTUP_TIMEOUT}s${NC}"
    echo -e "${YELLOW}Container logs:${NC}"
    docker logs --tail 50 $IBM_MQ_CONTAINER
    
    if [ "$CLEANUP_AFTER_TESTS" = true ]; then
        echo -e "${YELLOW}Stopping IBM MQ container...${NC}"
        docker-compose --profile ibmmq down
    fi
    
    exit 1
fi

echo -e "${GREEN}✓ IBM MQ is ready${NC}"
echo ""

# Set environment variables for tests
echo -e "${YELLOW}[4/6] Setting environment variables...${NC}"
export IBMMQ_QUEUE_MANAGER=QM1
export IBMMQ_HOST=localhost
export IBMMQ_PORT=1414
export IBMMQ_CHANNEL=DEV.APP.SVRCONN
export IBMMQ_QUEUE_NAME=DEV.QUEUE.1
export IBMMQ_USERNAME=app
export IBMMQ_PASSWORD=passw0rd

echo "IBMMQ_QUEUE_MANAGER=$IBMMQ_QUEUE_MANAGER"
echo "IBMMQ_HOST=$IBMMQ_HOST"
echo "IBMMQ_PORT=$IBMMQ_PORT"
echo "IBMMQ_CHANNEL=$IBMMQ_CHANNEL"
echo "IBMMQ_QUEUE_NAME=$IBMMQ_QUEUE_NAME"
echo -e "${GREEN}✓ Environment configured${NC}"
echo ""

# Run integration tests
echo -e "${YELLOW}[5/6] Running IBM MQ integration tests...${NC}"
echo ""

dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=IbmMQ" \
    --logger "console;verbosity=normal"

TEST_EXIT_CODE=$?

echo ""
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ All IBM MQ integration tests passed${NC}"
else
    echo -e "${RED}✗ Some IBM MQ integration tests failed (exit code: $TEST_EXIT_CODE)${NC}"
fi
echo ""

# Cleanup
echo -e "${YELLOW}[6/6] Cleanup...${NC}"
if [ "$CLEANUP_AFTER_TESTS" = true ]; then
    echo "Stopping IBM MQ container..."
    docker-compose --profile ibmmq down
    echo -e "${GREEN}✓ IBM MQ container stopped${NC}"
else
    echo "IBM MQ container is still running (use --cleanup flag to auto-stop)"
    echo "To stop manually: docker-compose --profile ibmmq down"
    echo "To view logs: docker logs $IBM_MQ_CONTAINER"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Test Execution Complete${NC}"
echo -e "${GREEN}========================================${NC}"

exit $TEST_EXIT_CODE