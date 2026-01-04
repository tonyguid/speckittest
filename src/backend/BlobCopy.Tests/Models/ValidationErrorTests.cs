using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for ValidationError model.
/// </summary>
public class ValidationErrorTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var error = new ValidationError();

        // Assert
        Assert.Equal(string.Empty, error.Field);
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.Code);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        // Arrange
        var error = new ValidationError();
        var field = "sourceUri";
        var message = "Source URI is not a valid Azure Blob Storage URI";
        var code = "INVALID_URI_FORMAT";

        // Act
        error.Field = field;
        error.Message = message;
        error.Code = code;

        // Assert
        Assert.Equal(field, error.Field);
        Assert.Equal(message, error.Message);
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void CanRepresentSourceUriError()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            Field = "sourceUri",
            Message = "Source blob does not exist",
            Code = "SOURCE_NOT_FOUND"
        };

        // Assert
        Assert.Equal("sourceUri", error.Field);
        Assert.Contains("does not exist", error.Message);
        Assert.Equal("SOURCE_NOT_FOUND", error.Code);
    }

    [Fact]
    public void CanRepresentDestinationUriError()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            Field = "destinationUri",
            Message = "Destination container is not accessible",
            Code = "DESTINATION_INACCESSIBLE"
        };

        // Assert
        Assert.Equal("destinationUri", error.Field);
        Assert.Contains("not accessible", error.Message);
        Assert.Equal("DESTINATION_INACCESSIBLE", error.Code);
    }

    [Fact]
    public void CanRepresentIdenticalUrisError()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            Field = "destinationUri",
            Message = "Source and destination URIs cannot be identical",
            Code = "IDENTICAL_URIS"
        };

        // Assert
        Assert.Equal("destinationUri", error.Field);
        Assert.Contains("identical", error.Message);
        Assert.Equal("IDENTICAL_URIS", error.Code);
    }
}
