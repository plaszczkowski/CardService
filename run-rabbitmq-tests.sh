# scripts/run-rabbitmq-tests.sh

#!/bin/bash

# Set environment variables for RabbitMQ integration tests
export RABBITMQ_HOST=localhost
export RABBITMQ_PORT=5672
export RABBITMQ_USERNAME=cardactions
export RABBITMQ_PASSWORD=devpassword

# Run integration tests
dotnet test --filter "Category=Integration & Requires=RabbitMQ"
