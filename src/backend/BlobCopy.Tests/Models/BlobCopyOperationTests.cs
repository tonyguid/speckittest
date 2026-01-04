using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for BlobCopyOperation model.
/// </summary>
public class BlobCopyOperationTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var operation = new BlobCopyOperation();

        // Assert
        Assert.Equal(string.Empty, operation.Id);
        Assert.Equal(string.Empty, operation.SourceUri);
        Assert.Equal(string.Empty, operation.DestinationUri);
        Assert.Equal(BlobCopyStatus.Pending, operation.Status);
        Assert.Equal(0m, operation.ProgressPercentage);
        Assert.Equal(0, operation.BytesTransferred);
        Assert.Equal(0, operation.TotalBytes);
        Assert.Null(operation.EstimatedTimeRemaining);
        Assert.Null(operation.ErrorMessage);
        Assert.Null(operation.ErrorCode);
        Assert.Null(operation.CompletedAt);
        Assert.False(operation.CancellationRequested);
    }

    [Fact]
    public void ProgressPercentage_CanSetValues_0To100()
    {
        // Arrange
        var operation = new BlobCopyOperation();

        // Act
        operation.ProgressPercentage = 50.5m;

        // Assert
        Assert.Equal(50.5m, operation.ProgressPercentage);
    }

    [Fact]
    public void Status_CanSetAllEnumValues()
    {
        // Arrange
        var operation = new BlobCopyOperation();

        // Act & Assert
        foreach (BlobCopyStatus status in Enum.GetValues(typeof(BlobCopyStatus)))
        {
            operation.Status = status;
            Assert.Equal(status, operation.Status);
        }
    }

    [Fact]
    public void BytesTransferred_CanSetLargeValues()
    {
        // Arrange
        var operation = new BlobCopyOperation();
        long largeValue = 1073741824; // 1 GB

        // Act
        operation.BytesTransferred = largeValue;

        // Assert
        Assert.Equal(largeValue, operation.BytesTransferred);
    }

    [Fact]
    public void CompletedAt_CanBeSet()
    {
        // Arrange
        var operation = new BlobCopyOperation();
        var completedTime = DateTime.UtcNow;

        // Act
        operation.CompletedAt = completedTime;

        // Assert
        Assert.Equal(completedTime, operation.CompletedAt);
    }

    [Fact]
    public void ErrorFields_CanBeSetTogether()
    {
        // Arrange
        var operation = new BlobCopyOperation();
        var errorMessage = "Source blob not found";
        var errorCode = "SOURCE_NOT_FOUND";

        // Act
        operation.ErrorMessage = errorMessage;
        operation.ErrorCode = errorCode;
        operation.Status = BlobCopyStatus.Failed;

        // Assert
        Assert.Equal(errorMessage, operation.ErrorMessage);
        Assert.Equal(errorCode, operation.ErrorCode);
        Assert.Equal(BlobCopyStatus.Failed, operation.Status);
    }

    [Fact]
    public void CancellationRequested_CanBeSetTrue()
    {
        // Arrange
        var operation = new BlobCopyOperation();

        // Act
        operation.CancellationRequested = true;

        // Assert
        Assert.True(operation.CancellationRequested);
    }
}
