namespace BlobCopy.API.Models;

/// <summary>
/// Enumeration of all possible states for a blob copy operation.
/// </summary>
public enum BlobCopyStatus
{
    /// <summary>
    /// Operation created but not yet started.
    /// Validation has passed; waiting for copy to begin.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Copy operation is actively transferring data.
    /// Progress updates are being sent via SignalR.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Copy completed successfully.
    /// Destination blob is fully available and matches source.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Copy operation failed.
    /// Check ErrorMessage and ErrorCode for details.
    /// Retry is available unless it's a permanent error (auth, not found, etc.).
    /// </summary>
    Failed = 4,

    /// <summary>
    /// User cancelled the operation.
    /// May have transferred partial data depending on cancellation timing.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// The requested operation or resource was not found.
    /// Used for 404 responses.
    /// </summary>
    NotFound = 6
}
