using Azure.Storage.Blobs;

namespace BlobCopy.API.Services;

/// <summary>
/// Factory for creating BlobClient instances.
/// Allows for mocking in unit tests.
/// </summary>
public interface IBlobClientFactory
{
    /// <summary>
    /// Creates a BlobClient for the specified URI.
    /// </summary>
    /// <param name="blobUri">URI of the blob</param>
    /// <returns>BlobClient instance</returns>
    BlobClient CreateBlobClient(string blobUri);
}
