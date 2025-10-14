using CardActions.Application.Interfaces;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CardActions.API.Telemetry;

public class CardActionsMetrics : ICardActionsMetrics
{
    private readonly Counter<int> _requestsCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<int> _errorsCounter;
    private readonly Counter<int> _testFailuresCounter;
    private readonly ObservableGauge<int> _successRateGauge;

    private int _totalTests;
    private int _successfulTests;

    public CardActionsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("CardActions.API");

        _requestsCounter = meter.CreateCounter<int>(
            "cardactions.requests.count",
            description: "Count of card actions requests");

        _requestDuration = meter.CreateHistogram<double>(
            "cardactions.requests.duration",
            unit: "ms",
            description: "Duration of card actions requests");

        _errorsCounter = meter.CreateCounter<int>(
            "cardactions.errors.count",
            description: "Count of errors in card actions requests");

        _testFailuresCounter = meter.CreateCounter<int>(
            "cardactions.tests.failures",
            description: "Count of test failures");

        _successRateGauge = meter.CreateObservableGauge<int>(
            "cardactions.tests.success_rate",
            observeValue: () => new Measurement<int>(
                _totalTests > 0 ? (_successfulTests * 100) / _totalTests : 100,
                new KeyValuePair<string, object?>("type", "api_tests")),
            description: "Success rate of API tests in percentage");
    }

    public void RecordRequest(string cardType, string cardStatus, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "card_type", cardType },
            { "card_status", cardStatus },
            { "success", success }
        };

        _requestsCounter.Add(1, tags);
        _requestDuration.Record(durationMs, tags);

        if (!success)
        {
            _errorsCounter.Add(1, tags);
        }
    }

    public void RecordTestResult(bool success, string scenarioName)
    {
        _totalTests++;
        if (success)
        {
            _successfulTests++;
        }
        else
        {
            _testFailuresCounter.Add(1, new KeyValuePair<string, object?>("scenario", scenarioName));
        }
    }

    public void RecordCriticalFailure(string failureType, string description)
    {
        _testFailuresCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("failure_type", failureType),
            new("description", description)
        });
    }
}