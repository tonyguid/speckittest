namespace BlobCopy.API.Models;

/// <summary>
/// Details about a validation failure.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Field that failed validation.
    /// e.g., "sourceUri", "destinationUri"
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Error message describing the validation failure.
    /// User-friendly, actionable.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error code for programmatic handling.
    /// e.g., "INVALID_URI_FORMAT", "URI_NOT_FOUND", "INSUFFICIENT_PERMISSIONS"
    /// </summary>
    public string Code { get; set; } = string.Empty;
}
