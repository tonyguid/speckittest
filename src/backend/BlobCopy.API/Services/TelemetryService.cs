using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using BlobCopy.API.Models;

namespace BlobCopy.API.Services;

/// <summary>
/// Service for tracking telemetry events and metrics related to blob copy operations.
/// Integrates with Azure Application Insights for distributed tracing and performance monitoring.
/// </summary>
public class TelemetryService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(TelemetryClient telemetryClient, ILogger<TelemetryService> logger)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Tracks the start of a blob copy operation.
    /// </summary>
    public void TrackCopyOperationStarted(BlobCopyOperation operation)
    {
        var properties = new Dictionary<string, string>
        {
            { "OperationId", operation.Id },
            { "SourceUri", MaskUri(operation.SourceUri) },
            { "DestinationUri", MaskUri(operation.DestinationUri) },
            { "InitiatedBy", operation.InitiatedBy ?? "Unknown" },
            { "CorrelationId", operation.CorrelationId }
        };

        var metrics = new Dictionary<string, double>
        {
            { "TotalBytes", operation.TotalBytes }
        };

        _telemetryClient.TrackEvent("BlobCopyStarted", properties, metrics);
        _logger.LogInformation(
            "Blob copy operation started: {OperationId}, Size: {TotalBytes} bytes",
            operation.Id,
            operation.TotalBytes
        );
    }

    /// <summary>
    /// Tracks progress updates for a blob copy operation.
    /// </summary>
    public void TrackProgressUpdate(ProgressUpdate update)
    {
        var properties = new Dictionary<string, string>
        {
            { "OperationId", update.CopyOperationId }
        };

        var metrics = new Dictionary<string, double>
        {
            { "BytesTransferred", update.BytesTransferred },
            { "TotalBytes", update.TotalBytes },
            { "ProgressPercentage", update.ProgressPercentage },
            { "TransferRateMbps", update.TransferRateMbps ?? 0 },
            { "EstimatedSecondsRemaining", update.EstimatedSecondsRemaining ?? 0 }
        };

        _telemetryClient.TrackEvent("BlobCopyProgress", properties, metrics);
    }

    /// <summary>
    /// Tracks successful completion of a blob copy operation.
    /// </summary>
    public void TrackCopyOperationCompleted(BlobCopyOperation operation, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            { "OperationId", operation.Id },
            { "SourceUri", MaskUri(operation.SourceUri) },
            { "DestinationUri", MaskUri(operation.DestinationUri) },
            { "CorrelationId", operation.CorrelationId }
        };

        var metrics = new Dictionary<string, double>
        {
            { "BytesCopied", operation.BytesCopied ?? 0 },
            { "TotalBytes", operation.TotalBytes },
            { "DurationSeconds", duration.TotalSeconds },
            { "ThroughputMbps", CalculateThroughput(operation.BytesCopied ?? 0, duration) }
        };

        _telemetryClient.TrackEvent("BlobCopyCompleted", properties, metrics);
        _logger.LogInformation(
            "Blob copy operation completed successfully: {OperationId}, Duration: {Duration}ms",
            operation.Id,
            duration.TotalMilliseconds
        );
    }

    /// <summary>
    /// Tracks failure of a blob copy operation.
    /// </summary>
    public void TrackCopyOperationFailed(BlobCopyOperation operation, string errorMessage, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            { "OperationId", operation.Id },
            { "SourceUri", MaskUri(operation.SourceUri) },
            { "DestinationUri", MaskUri(operation.DestinationUri) },
            { "ErrorMessage", errorMessage },
            { "ErrorCode", operation.ErrorCode ?? "Unknown" },
            { "CorrelationId", operation.CorrelationId }
        };

        var metrics = new Dictionary<string, double>
        {
            { "BytesCopied", operation.BytesCopied ?? 0 },
            { "TotalBytes", operation.TotalBytes },
            { "DurationSeconds", duration.TotalSeconds }
        };

        _telemetryClient.TrackEvent("BlobCopyFailed", properties, metrics);
        _logger.LogError(
            "Blob copy operation failed: {OperationId}, Error: {ErrorMessage}, Duration: {Duration}ms",
            operation.Id,
            errorMessage,
            duration.TotalMilliseconds
        );
    }

    /// <summary>
    /// Tracks cancellation of a blob copy operation.
    /// </summary>
    public void TrackCopyOperationCancelled(BlobCopyOperation operation, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            { "OperationId", operation.Id },
            { "SourceUri", MaskUri(operation.SourceUri) },
            { "DestinationUri", MaskUri(operation.DestinationUri) },
            { "CorrelationId", operation.CorrelationId }
        };

        var metrics = new Dictionary<string, double>
        {
            { "BytesCopied", operation.BytesCopied ?? 0 },
            { "TotalBytes", operation.TotalBytes },
            { "DurationSeconds", duration.TotalSeconds }
        };

        _telemetryClient.TrackEvent("BlobCopyCancelled", properties, metrics);
        _logger.LogWarning(
            "Blob copy operation cancelled: {OperationId}, Bytes copied: {BytesCopied}, Duration: {Duration}ms",
            operation.Id,
            operation.BytesCopied,
            duration.TotalMilliseconds
        );
    }

    /// <summary>
    /// Tracks validation errors.
    /// </summary>
    public void TrackValidationError(string sourceUri, string? errorCode, string errorMessage)
    {
        var properties = new Dictionary<string, string>
        {
            { "SourceUri", MaskUri(sourceUri) },
            { "ErrorCode", errorCode ?? "Unknown" },
            { "ErrorMessage", errorMessage }
        };

        _telemetryClient.TrackEvent("BlobCopyValidationError", properties);
        _logger.LogWarning(
            "Blob copy validation error: URI: {SourceUri}, Code: {ErrorCode}, Message: {ErrorMessage}",
            MaskUri(sourceUri),
            errorCode,
            errorMessage
        );
    }

    /// <summary>
    /// Tracks API endpoint latency.
    /// </summary>
    public void TrackApiLatency(string endpoint, TimeSpan duration, int statusCode)
    {
        var properties = new Dictionary<string, string>
        {
            { "Endpoint", endpoint },
            { "StatusCode", statusCode.ToString() }
        };

        var metrics = new Dictionary<string, double>
        {
            { "DurationMs", duration.TotalMilliseconds }
        };

        _telemetryClient.TrackEvent("ApiLatency", properties, metrics);
    }

    /// <summary>
    /// Tracks custom metric for monitoring.
    /// </summary>
    public void TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null)
    {
        _telemetryClient.GetMetricManager().Publish(metricName, value);
        _logger.LogDebug("Metric tracked: {MetricName} = {Value}", metricName, value);
    }

    /// <summary>
    /// Masks sensitive parts of a URI for logging.
    /// </summary>
    private static string MaskUri(string uri)
    {
        try
        {
            var uriObj = new Uri(uri);
            return $"{uriObj.Scheme}://{uriObj.Host}/{string.Join("/", uriObj.Segments.Skip(1).Take(2))}/*";
        }
        catch
        {
            return "***";
        }
    }

    /// <summary>
    /// Calculates throughput in MB/s.
    /// </summary>
    private static double CalculateThroughput(long bytes, TimeSpan duration)
    {
        if (duration.TotalSeconds == 0) return 0;
        var megabytes = bytes / (1024.0 * 1024.0);
        return megabytes / duration.TotalSeconds;
    }
}
