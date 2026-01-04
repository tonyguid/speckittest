using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for ProgressUpdate model.
/// </summary>
public class ProgressUpdateTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var update = new ProgressUpdate();

        // Assert
        Assert.Equal(string.Empty, update.CopyOperationId);
        Assert.Equal(0, update.BytesTransferred);
        Assert.Equal(0, update.TotalBytes);
        Assert.Equal(0m, update.ProgressPercentage);
        Assert.Null(update.EstimatedSecondsRemaining);
        Assert.Equal(0m, update.TransferRateMbps);
    }

    [Fact]
    public void ProgressPercentage_CanBeSet()
    {
        // Arrange
        var update = new ProgressUpdate();

        // Act
        update.ProgressPercentage = 75.5m;

        // Assert
        Assert.Equal(75.5m, update.ProgressPercentage);
    }

    [Fact]
    public void TransferRate_CanBeSet()
    {
        // Arrange
        var update = new ProgressUpdate();
        decimal transferRate = 25.5m;

        // Act
        update.TransferRateMbps = transferRate;

        // Assert
        Assert.Equal(transferRate, update.TransferRateMbps);
    }

    [Fact]
    public void EstimatedSecondsRemaining_CanBeNull()
    {
        // Arrange
        var update = new ProgressUpdate();

        // Act & Assert
        Assert.Null(update.EstimatedSecondsRemaining);
    }

    [Fact]
    public void EstimatedSecondsRemaining_CanBeSet()
    {
        // Arrange
        var update = new ProgressUpdate();

        // Act
        update.EstimatedSecondsRemaining = 45;

        // Assert
        Assert.Equal(45, update.EstimatedSecondsRemaining);
    }

    [Fact]
    public void BytesTransferred_AndTotalBytes_CanBeLarge()
    {
        // Arrange
        var update = new ProgressUpdate();
        long largeBytes = 10737418240; // 10 GB

        // Act
        update.BytesTransferred = largeBytes / 2;
        update.TotalBytes = largeBytes;

        // Assert
        Assert.Equal(largeBytes / 2, update.BytesTransferred);
        Assert.Equal(largeBytes, update.TotalBytes);
    }

    [Fact]
    public void UpdatedAt_DefaultsToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var update = new ProgressUpdate();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(update.UpdatedAt, beforeCreation, afterCreation.AddSeconds(1));
    }
}
