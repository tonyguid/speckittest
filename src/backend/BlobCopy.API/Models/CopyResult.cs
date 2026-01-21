namespace BlobCopy.API.Models;

/// <summary>
/// Result of a completed copy operation.
/// </summary>
public class CopyResult
{
    /// <summary>
    /// Whether the copy operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The destination URI where the blob was copied.
    /// </summary>
    public string? DestinationUri { get; set; }

    /// <summary>
    /// Error message if the copy failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Duration of the copy operation in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Creates a successful copy result.
    /// </summary>
    public static CopyResult Succeeded(string destinationUri, long bytesTransferred, long durationMs) => new()
    {
        Success = true,
        DestinationUri = destinationUri,
        BytesTransferred = bytesTransferred,
        DurationMs = durationMs
    };

    /// <summary>
    /// Creates a failed copy result.
    /// </summary>
    public static CopyResult Failed(string errorMessage, string errorCode) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
