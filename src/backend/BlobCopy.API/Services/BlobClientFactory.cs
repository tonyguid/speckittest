using Azure.Storage.Blobs;

namespace BlobCopy.API.Services;

/// <summary>
/// Default implementation of IBlobClientFactory.
/// Creates BlobClient instances using Azure Storage SDK.
/// </summary>
public class BlobClientFactory : IBlobClientFactory
{
    public BlobClient CreateBlobClient(string blobUri)
    {
        return new BlobClient(new Uri(blobUri));
    }
}
