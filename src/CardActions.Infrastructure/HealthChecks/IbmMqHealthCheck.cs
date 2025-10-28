using CardActions.Infrastructure.Configuration;
using IBM.WMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections;

namespace CardActions.Infrastructure.HealthChecks;

/// <summary>
/// Health check for IBM MQ connectivity and queue availability.
/// Verifies connection to Queue Manager and accessibility of target queue.
/// </summary>
public class IbmMqHealthCheck : IHealthCheck
{
    private readonly IbmMqOptions _options;

    /// <summary>
    /// Initializes IBM MQ health check with configuration.
    /// </summary>
    /// <param name="options">IBM MQ configuration options</param>
    /// <exception cref="ArgumentNullException">Thrown if options is null</exception>
    public IbmMqHealthCheck(IbmMqOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Checks IBM MQ health by attempting connection and queue verification.
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result with status and details</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        MQQueueManager? queueManager = null;
        MQQueue? queue = null;

        try
        {
            // Build connection properties
            var properties = new Hashtable
            {
                { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_CLIENT },
                { MQC.HOST_NAME_PROPERTY, _options.Host },
                { MQC.PORT_PROPERTY, _options.Port },
                { MQC.CHANNEL_PROPERTY, _options.Channel }
            };

            // Add authentication if provided
            if (!string.IsNullOrEmpty(_options.Username))
            {
                properties.Add(MQC.USER_ID_PROPERTY, _options.Username);
            }

            if (!string.IsNullOrEmpty(_options.Password))
            {
                properties.Add(MQC.PASSWORD_PROPERTY, _options.Password);
            }

            // Add SSL configuration if enabled
            if (_options.UseSsl)
            {
                properties.Add(MQC.SSL_CIPHER_SPEC_PROPERTY, _options.SslCipherSpec);
            }

            // Connect to Queue Manager (IBM MQ .NET doesn't have true async)
            queueManager = await Task.Run(
                () => new MQQueueManager(_options.QueueManager, properties),
                cancellationToken);

            // Verify connection is established
            if (!queueManager.IsConnected)
            {
                return HealthCheckResult.Unhealthy(
                    $"IBM MQ Queue Manager '{_options.QueueManager}' is not connected");
            }

            // Verify queue accessibility (open for inquiry only - no get/put)
            var openOptions = MQC.MQOO_INQUIRE | MQC.MQOO_FAIL_IF_QUIESCING;
            queue = await Task.Run(
                () => queueManager.AccessQueue(_options.QueueName, openOptions),
                cancellationToken);

            // Get queue depth (current number of messages)
            var currentDepth = queue.CurrentDepth;

            // Build health check data
            var data = new Dictionary<string, object>
            {
                { "queueManager", _options.QueueManager },
                { "host", $"{_options.Host}:{_options.Port}" },
                { "channel", _options.Channel },
                { "queue", _options.QueueName },
                { "queueDepth", currentDepth },
                { "connected", queueManager.IsConnected }
            };

            return HealthCheckResult.Healthy(
                $"IBM MQ connection verified - Queue Manager: {_options.QueueManager}, Queue: {_options.QueueName}, Depth: {currentDepth}",
                data);
        }
        catch (MQException mqEx)
        {
            // IBM MQ specific error handling
            var reasonText = GetReasonText(mqEx.ReasonCode);
            var data = new Dictionary<string, object>
            {
                { "queueManager", _options.QueueManager },
                { "host", $"{_options.Host}:{_options.Port}" },
                { "reasonCode", mqEx.ReasonCode },
                { "reasonText", reasonText },
                { "completionCode", mqEx.CompletionCode }
            };

            // Determine if degraded or unhealthy
            var status = IsDegradedError(mqEx.ReasonCode)
                ? HealthStatus.Degraded
                : HealthStatus.Unhealthy;

            return new HealthCheckResult(
                status,
                $"IBM MQ health check failed: {reasonText} (ReasonCode: {mqEx.ReasonCode})",
                mqEx,
                data);
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "IBM MQ health check timed out",
                data: new Dictionary<string, object>
                {
                    { "queueManager", _options.QueueManager },
                    { "host", $"{_options.Host}:{_options.Port}" },
                    { "timeout", true }
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"IBM MQ health check failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    { "queueManager", _options.QueueManager },
                    { "host", $"{_options.Host}:{_options.Port}" }
                });
        }
        finally
        {
            // Cleanup resources
            try
            {
                queue?.Close();
            }
            catch { /* Ignore cleanup errors */ }

            try
            {
                if (queueManager != null && queueManager.IsConnected)
                {
                    queueManager.Disconnect();
                }
                queueManager?.Close();
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// Determines if MQ error code should result in Degraded vs Unhealthy status.
    /// Degraded: Service partially available (e.g., queue full, but connectable).
    /// Unhealthy: Service unavailable (e.g., connection refused).
    /// </summary>
    private static bool IsDegradedError(int reasonCode)
    {
        return reasonCode switch
        {
            MQC.MQRC_Q_FULL => true,                    // Queue full - degraded but connectable
            MQC.MQRC_Q_MGR_QUIESCING => true,           // Queue Manager shutting down gracefully
            MQC.MQRC_CONNECTION_QUIESCING => true,      // Connection being closed gracefully
            _ => false                                   // All other errors are unhealthy
        };
    }

    /// <summary>
    /// Converts IBM MQ reason code to human-readable text.
    /// </summary>
    private static string GetReasonText(int reasonCode)
    {
        return reasonCode switch
        {
            MQC.MQRC_CONNECTION_BROKEN => "Connection broken",
            MQC.MQRC_CONNECTION_ERROR => "Connection error",
            MQC.MQRC_Q_MGR_NOT_AVAILABLE => "Queue Manager not available",
            MQC.MQRC_Q_MGR_QUIESCING => "Queue Manager quiescing",
            MQC.MQRC_CONNECTION_NOT_AUTHORIZED => "Connection not authorized",
            MQC.MQRC_Q_FULL => "Queue full",
            MQC.MQRC_UNKNOWN_OBJECT_NAME => "Unknown queue name",
            MQC.MQRC_NOT_AUTHORIZED => "Not authorized",
            MQC.MQRC_HOST_NOT_AVAILABLE => "Host not available",
            _ => $"MQ Error {reasonCode}"
        };
    }
}