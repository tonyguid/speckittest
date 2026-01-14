namespace BlobCopy.API.Models;

/// <summary>
/// Request payload for initiating a blob copy operation.
/// </summary>
public class CopyRequest
{
    /// <summary>
    /// Source blob URI in Azure Blob Storage.
    /// Format: https://[account].blob.core.windows.net/[container]/[blob]
    /// Required, must be a valid Azure Blob Storage URI.
    /// </summary>
    public string SourceUri { get; set; } = string.Empty;

    /// <summary>
    /// Destination blob URI in Azure Blob Storage.
    /// Format: https://[account].blob.core.windows.net/[container]/[blob]
    /// Required, must be a valid Azure Blob Storage URI.
    /// Must be different from SourceUri.
    /// </summary>
    public string DestinationUri { get; set; } = string.Empty;

    /// <summary>
    /// If true, overwrite destination blob if it already exists.
    /// If false and destination exists, returns 409 Conflict.
    /// Default: false
    /// </summary>
    public bool OverwriteIfExists { get; set; } = false;

    /// <summary>
    /// Optional new name for the destination blob when resolving a conflict.
    /// If provided, the copy will use this name instead of the original.
    /// </summary>
    public string? NewDestinationName { get; set; }
}
