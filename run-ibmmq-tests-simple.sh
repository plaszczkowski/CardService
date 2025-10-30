#!/bin/bash

# run-ibmmq-tests-simple.sh
# Simplified IBM MQ integration test runner
# Assumes IBM MQ is already running

# Set environment variables for IBM MQ integration tests
export IBMMQ_QUEUE_MANAGER=QM1
export IBMMQ_HOST=localhost
export IBMMQ_PORT=1414
export IBMMQ_CHANNEL=DEV.APP.SVRCONN
export IBMMQ_QUEUE_NAME=DEV.QUEUE.1
export IBMMQ_USERNAME=app
export IBMMQ_PASSWORD=passw0rd

# Run integration tests
dotnet test tests/CardActions.IntegrationTests \
    --filter "Category=Integration & Requires=IbmMQ"