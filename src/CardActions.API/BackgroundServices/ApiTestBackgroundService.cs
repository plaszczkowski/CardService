using CardActions.Infrastructure.Diagnostics;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace CardActions.API.BackgroundServices;

public class ApiTestBackgroundService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiTestBackgroundService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly TestConfiguration _testConfig;
    private readonly Meter _meter;
    private readonly Counter<int> _testErrorsCounter;
    private readonly IStartupAudit _audit;

    public ApiTestBackgroundService(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiTestBackgroundService> logger,
        IHostEnvironment environment,
        IOptions<TestConfiguration> testConfig,
        IStartupAudit audit)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
        _testConfig = testConfig.Value;

        _meter = new Meter("CardActions.API.Tests");
        _testErrorsCounter = _meter.CreateCounter<int>(
            "cardactions.tests.errors",
            description: "Count of test errors");
        _audit = audit;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sprawdź czy testy są włączone w konfiguracji
        if (!_testConfig.Enabled)
        {
            _logger.LogInformation("Automated API tests are disabled");
            return;
        }

        // Tylko w środowisku Development i Staging
        if (!_environment.IsDevelopment() && !_environment.IsStaging())
        {
            _logger.LogInformation("Skipping automated API tests in production environment");
            return;
        }

        // Poczekaj aż API się w pełni uruchomi
        await Task.Delay(_testConfig.StartupDelayMs, stoppingToken);

        _audit.VerifyAssemblyVersion("RabbitMQ.Client", "6.8.1.0");

        _logger.LogInformation("STARTING AUTOMATED API TESTS");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_testConfig.BaseUrl);

        var testResults = new List<TestResult>();

        try
        {
            testResults = await RunTestScenarios(client, stoppingToken);
            await AnalyzeTestResults(testResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR during automated API testing");
            _testErrorsCounter.Add(1, new KeyValuePair<string, object?>("error_type", "test_execution_failed"));
        }

        _logger.LogInformation("AUTOMATED API TESTS COMPLETED");
    }

    private async Task<List<TestResult>> RunTestScenarios(HttpClient client, CancellationToken cancellationToken)
    {
        // Definiuj scenariusze w zależności od kategorii
        var allScenarios = new[]
        {
            new {
                Name = "Valid Prepaid Closed",
                UserId = "User1",
                CardNumber = "PREPAID_CLOSED",
                ExpectedStatus = 200,
                Category = TestCategory.Smoke
            },
            new {
                Name = "Valid Credit Blocked PIN",
                UserId = "User1",
                CardNumber = "CREDIT_BLOCKED_PIN",
                ExpectedStatus = 200,
                Category = TestCategory.Functional
            },
            new {
                Name = "Non-existent Card",
                UserId = "User1",
                CardNumber = "NON_EXISTENT_123",
                ExpectedStatus = 404,
                Category = TestCategory.Functional
            },
            new {
                Name = "Unauthorized Access",
                UserId = "User1",
                CardNumber = "Card21",
                ExpectedStatus = 403,
                Category = TestCategory.Functional
            },
            new {
                Name = "Invalid UserId",
                UserId = "Us",
                CardNumber = "CARD123",
                ExpectedStatus = 400,
                Category = TestCategory.Functional
            },
            new {
                Name = "Empty CardNumber",
                UserId = "User1",
                CardNumber = "",
                ExpectedStatus = 400,
                Category = TestCategory.Functional
            },
            new {
                Name = "Invalid Characters",
                UserId = "User1",
                CardNumber = "INVALID!@#",
                ExpectedStatus = 400,
                Category = TestCategory.Functional
            }
        };

        // Filtruj scenariusze na podstawie konfiguracji
        var scenariosToRun = allScenarios
            .Where(s => _testConfig.TestCategories.Contains(s.Category)).ToArray();

        _logger.LogInformation("Running {ScenarioCount} test scenarios from categories: {Categories}",
            scenariosToRun.Length, string.Join(", ", _testConfig.TestCategories));

        var results = new List<TestResult>();

        foreach (var scenario in scenariosToRun)
        {
            var result = await ExecuteTestScenarioWithRetry(client, scenario.Name, scenario.UserId, scenario.CardNumber, scenario.ExpectedStatus, cancellationToken);
            results.Add(result);
            await Task.Delay(_testConfig.DelayBetweenTestsMs, cancellationToken);
        }

        return results;
    }

    private async Task<TestResult> ExecuteTestScenarioWithRetry(
        HttpClient client, string scenarioName, string userId, string cardNumber, int expectedStatus, CancellationToken cancellationToken)
    {
        const int maxRetries = 2;
        const int retryDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            var result = await ExecuteTestScenario(client, scenarioName, userId, cardNumber, expectedStatus, cancellationToken);

            // Jeśli test przeszedł lub to ostatnia próba - zwróć wynik
            if (result.Success || attempt > maxRetries)
            {
                if (attempt > 1 && result.Success)
                {
                    _logger.LogInformation("   RETRY SUCCESS: Scenario {ScenarioName} passed on attempt {Attempt}",
                        scenarioName, attempt);
                }
                return result;
            }

            _logger.LogWarning("   RETRY: Attempt {Attempt}/{MaxRetries} for scenario: {ScenarioName}",
                attempt, maxRetries, scenarioName);
            await Task.Delay(retryDelayMs, cancellationToken);
        }

        // Ten kod nie powinien się wykonać, ale na wypadek błędu
        return new TestResult
        {
            ScenarioName = scenarioName,
            ExpectedStatus = expectedStatus,
            ActualStatus = -1,
            Success = false,
            Error = "All retry attempts failed unexpectedly"
        };
    }

    private async Task<TestResult> ExecuteTestScenario(
        HttpClient client, string scenarioName, string userId, string cardNumber, int expectedStatus, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new TestResult { ScenarioName = scenarioName, ExpectedStatus = expectedStatus };

        try
        {
            // Użyj timeout per test
            using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, testTimeout.Token);

            _logger.LogInformation("Testing: {ScenarioName}", scenarioName);
            _logger.LogInformation("   Endpoint: /api/v1/cards/actions?userId={UserId}&cardNumber={CardNumber}", userId, cardNumber);

            var response = await client.GetAsync($"/api/v1/cards/actions?userId={userId}&cardNumber={cardNumber}", linkedCts.Token);
            var content = await response.Content.ReadAsStringAsync(linkedCts.Token);

            stopwatch.Stop();

            result.ActualStatus = (int)response.StatusCode;
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Success = result.ActualStatus == expectedStatus;
            result.ResponseContent = content;

            var statusResult = result.Success ? "SUCCESS" : "FAILED";

            _logger.LogInformation("   {StatusResult} Status: {StatusCode} (Expected: {ExpectedStatus}) {Duration}ms",
                statusResult, result.ActualStatus, expectedStatus, result.DurationMs);

            _logger.LogInformation("   Response: {Content}", content);

            if (!result.Success)
            {
                _logger.LogWarning("   STATUS MISMATCH! Expected {ExpectedStatus}, got {ActualStatus}",
                    expectedStatus, result.ActualStatus);
                _testErrorsCounter.Add(1, new KeyValuePair<string, object?>("scenario", scenarioName));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = "Test timeout (30 seconds)";
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError("   TIMEOUT in scenario: {ScenarioName} after {Duration}ms", scenarioName, result.DurationMs);
            _testErrorsCounter.Add(1, new KeyValuePair<string, object?>("scenario", scenarioName));
            _testErrorsCounter.Add(1, new KeyValuePair<string, object?>("error_type", "timeout"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "   ERROR in scenario: {ScenarioName}", scenarioName);
            _testErrorsCounter.Add(1, new KeyValuePair<string, object?>("scenario", scenarioName));
        }

        _logger.LogInformation("   ---");
        return result;
    }

    private async Task AnalyzeTestResults(List<TestResult> results)
    {
        var totalTests = results.Count;
        var failedTests = results.Count(r => !r.Success);
        var successRate = totalTests > 0 ? (double)(totalTests - failedTests) / totalTests * 100 : 0;

        _logger.LogInformation("TEST RESULTS ANALYSIS");
        _logger.LogInformation("Total Tests: {TotalTests}", totalTests);
        _logger.LogInformation("Passed: {PassedTests}", totalTests - failedTests);
        _logger.LogInformation("Failed: {FailedTests}", failedTests);
        _logger.LogInformation("Success Rate: {SuccessRate:F2}%", successRate);

        if (failedTests > 0)
        {
            _logger.LogWarning("FAILED TESTS DETAILS:");
            foreach (var failed in results.Where(r => !r.Success))
            {
                _logger.LogWarning("   - {Scenario}: Expected {Expected}, Got {Actual}, Error: {Error}",
                    failed.ScenarioName, failed.ExpectedStatus, failed.ActualStatus, failed.Error ?? "None");
            }
        }

        if (failedTests > 0 && successRate < _testConfig.CriticalSuccessRate)
        {
            await HandleTestFailures(results, failedTests, successRate);
        }
        else if (failedTests > 0)
        {
            _logger.LogWarning("Tests have failures but success rate {SuccessRate}% is above critical threshold {CriticalRate}%",
                successRate, _testConfig.CriticalSuccessRate);
        }
        else
        {
            _logger.LogInformation("ALL TESTS PASSED SUCCESSFULLY!");
        }
    }

    private async Task HandleTestFailures(List<TestResult> results, int failedTests, double successRate)
    {
        _logger.LogError("TEST FAILURES DETECTED: {FailedTests} out of {TotalTests} tests failed",
            failedTests, results.Count);

        // Action 1: Log critical error
        _logger.LogCritical("CRITICAL: API test success rate below threshold: {SuccessRate}%", successRate);

        // Action 2: Add custom metric for monitoring
        _testErrorsCounter.Add(failedTests, new KeyValuePair<string, object?>("failure_type", "test_failures"));

        // Action 3: Simulate sending alert
        await SendTestFailureAlert(results, failedTests, successRate);

        // Action 4: Optionally throw exception to stop the application
        if (successRate < 50.0) // If less than 50% tests pass
        {
            throw new InvalidOperationException(
                $"Critical test failures: {failedTests} tests failed. Success rate: {successRate:F2}%");
        }
    }

    private async Task SendTestFailureAlert(List<TestResult> results, int failedTests, double successRate)
    {
        try
        {
            // Simulate sending alert
            _logger.LogWarning("SENDING TEST FAILURE ALERT - Failed: {FailedTests}, Success Rate: {SuccessRate}%",
                failedTests, successRate);

            await Task.Delay(100); // Simulate async operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test failure alert");
        }
    }
}