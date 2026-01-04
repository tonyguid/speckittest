namespace BlobCopy.API.Models;

/// <summary>
/// Response payload when a copy operation fails validation or encounters an error.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// HTTP status code (mirrors the response status).
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Machine-readable error code.
    /// e.g., "INVALID_REQUEST", "VALIDATION_FAILED", "NOT_FOUND", "UNAUTHORIZED"
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing this specific error.
    /// Can be used to find logs in Application Insights.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// List of validation errors (if applicable).
    /// Null if this is not a validation error (400 Bad Request).
    /// </summary>
    public List<ValidationError>? ValidationErrors { get; set; } = null;

    /// <summary>
    /// Timestamp of the error (ISO 8601).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
