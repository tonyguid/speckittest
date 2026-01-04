using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for CopyRequest model.
/// </summary>
public class CopyRequestTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var request = new CopyRequest();

        // Assert
        Assert.Equal(string.Empty, request.SourceUri);
        Assert.Equal(string.Empty, request.DestinationUri);
    }

    [Fact]
    public void CanSetSourceUri()
    {
        // Arrange
        var request = new CopyRequest();
        var sourceUri = "https://example.blob.core.windows.net/source/blob.bin";

        // Act
        request.SourceUri = sourceUri;

        // Assert
        Assert.Equal(sourceUri, request.SourceUri);
    }

    [Fact]
    public void CanSetDestinationUri()
    {
        // Arrange
        var request = new CopyRequest();
        var destinationUri = "https://example.blob.core.windows.net/dest/blob.bin";

        // Act
        request.DestinationUri = destinationUri;

        // Assert
        Assert.Equal(destinationUri, request.DestinationUri);
    }

    [Fact]
    public void CanSetBothUrisInConstructor()
    {
        // Arrange & Act
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob.bin",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob.bin"
        };

        // Assert
        Assert.NotEmpty(request.SourceUri);
        Assert.NotEmpty(request.DestinationUri);
        Assert.NotEqual(request.SourceUri, request.DestinationUri);
    }
}
