using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardActions.Infrastructure.Configuration
{
    /// <summary>
    /// RabbitMQ-specific configuration options.
    /// </summary>
    public class RabbitMQOptions
    {
        /// <summary>
        /// RabbitMQ host (default: localhost for dev).
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// RabbitMQ port (default: 5672).
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// RabbitMQ username.
        /// </summary>
        public string Username { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ password (should come from secrets in production).
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ virtual host (default: /).
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Exchange name for publishing events.
        /// </summary>
        public string Exchange { get; set; } = "cardactions.events";

        public DeadLetterQueueOptions? DeadLetterQueue { get; set; }

        /// <summary>
        /// Validates RabbitMQ configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Host))
                throw new InvalidOperationException("EventBus:RabbitMQ:Host is required when using RabbitMQ.");

            if (Port <= 0 || Port > 65535)
                throw new InvalidOperationException($"EventBus:RabbitMQ:Port must be between 1 and 65535 (got {Port}).");

            if (string.IsNullOrWhiteSpace(Exchange))
                throw new InvalidOperationException("EventBus:RabbitMQ:Exchange is required.");
        }
    }
}
