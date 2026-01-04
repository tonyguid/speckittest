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
}
