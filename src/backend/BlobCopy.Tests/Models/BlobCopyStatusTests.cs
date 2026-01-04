using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for BlobCopyStatus enum.
/// </summary>
public class BlobCopyStatusTests
{
    [Theory]
    [InlineData(BlobCopyStatus.Pending, 1)]
    [InlineData(BlobCopyStatus.InProgress, 2)]
    [InlineData(BlobCopyStatus.Completed, 3)]
    [InlineData(BlobCopyStatus.Failed, 4)]
    [InlineData(BlobCopyStatus.Cancelled, 5)]
    public void Status_HasCorrectNumericValues(BlobCopyStatus status, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)status);
    }

    [Fact]
    public void AllStatusValues_AreUnique()
    {
        // Arrange
        var values = Enum.GetValues(typeof(BlobCopyStatus)).Cast<BlobCopyStatus>().ToList();
        var intValues = values.Select(s => (int)s).ToList();

        // Act & Assert
        Assert.Equal(intValues.Distinct().Count(), intValues.Count);
    }

    [Fact]
    public void Status_CanBeParsedFromString()
    {
        // Act
        var parsed = Enum.Parse<BlobCopyStatus>("Pending");

        // Assert
        Assert.Equal(BlobCopyStatus.Pending, parsed);
    }

    [Fact]
    public void Status_CanBeConvertedToString()
    {
        // Act
        var stringValue = BlobCopyStatus.InProgress.ToString();

        // Assert
        Assert.Equal("InProgress", stringValue);
    }
}
