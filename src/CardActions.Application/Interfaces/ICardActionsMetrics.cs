using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardActions.Application.Interfaces
{
    public interface ICardActionsMetrics
    {
        void RecordRequest(string cardType, string cardStatus, bool success, double durationMs);
    }
}
