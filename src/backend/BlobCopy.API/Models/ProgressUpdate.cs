namespace BlobCopy.API.Models;

/// <summary>
/// Data structure sent to clients via SignalR during active copy operations.
/// </summary>
public class ProgressUpdate
{
    /// <summary>
    /// Unique identifier of the copy operation.
    /// </summary>
    public string CopyOperationId { get; set; } = string.Empty;

    /// <summary>
    /// Bytes transferred in this interval.
    /// </summary>
    public long BytesTransferred { get; set; } = 0;

    /// <summary>
    /// Total bytes for the entire blob.
    /// </summary>
    public long TotalBytes { get; set; } = 0;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public decimal ProgressPercentage { get; set; } = 0m;

    /// <summary>
    /// Estimated seconds remaining (rounded).
    /// Null if indeterminate.
    /// </summary>
    public int? EstimatedSecondsRemaining { get; set; } = null;

    /// <summary>
    /// Timestamp of this progress update (ISO 8601).
    /// Used for client-side progress rate calculation.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transfer rate in MB/s.
    /// For display purposes.
    /// </summary>
    public decimal TransferRateMbps { get; set; } = 0m;
}
