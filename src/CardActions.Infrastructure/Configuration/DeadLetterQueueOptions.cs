using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardActions.Infrastructure.Configuration
{
    public class DeadLetterQueueOptions
    {
        public bool Enabled { get; set; } = true;
        public string Exchange { get; set; } = "cardactions.events.dlq";
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 5000;
    }
}
