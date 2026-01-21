namespace BlobCopy.API.Models;

/// <summary>
/// Response returned when destination blob already exists (409 Conflict).
/// </summary>
public class ConflictResponse
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string Code { get; set; } = "DESTINATION_EXISTS";

    /// <summary>
    /// User-friendly error message.
    /// </summary>
    public string Message { get; set; } = "Destination blob already exists.";

    /// <summary>
    /// The destination URI that already exists.
    /// </summary>
    public string DestinationUri { get; set; } = string.Empty;

    /// <summary>
    /// Whether the destination can be overwritten.
    /// </summary>
    public bool CanOverwrite { get; set; } = true;

    /// <summary>
    /// Suggested alternative actions.
    /// </summary>
    public List<string> SuggestedActions { get; set; } = new()
    {
        "Set overwriteIfExists to true to overwrite",
        "Provide a new destination name using newDestinationName",
        "Cancel the operation"
    };

    /// <summary>
    /// Creates a conflict response for an existing destination.
    /// </summary>
    public static ConflictResponse ForExistingDestination(string destinationUri) => new()
    {
        DestinationUri = destinationUri,
        Message = $"A blob already exists at '{destinationUri}'. Choose to overwrite, rename, or cancel."
    };
}
