using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;
using BlobCopy.API.Extensions;

namespace BlobCopy.Tests.Extensions;

/// <summary>
/// Unit tests for AzureStorageServiceCollectionExtensions.
/// </summary>
public class AzureStorageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAzureStorageServices_WithValidUri_RegistersBlobServiceClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var uri = new Uri("https://example.blob.core.windows.net");

        // Act
        services.AddAzureStorageServices(uri);
        var provider = services.BuildServiceProvider();

        // Assert
        var client = provider.GetService<BlobServiceClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddAzureStorageServices_WithNullUri_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddAzureStorageServices(null!)
        );
    }

    [Fact]
    public void AddAzureStorageServices_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;
        var uri = new Uri("https://example.blob.core.windows.net");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services!.AddAzureStorageServices(uri)
        );
    }

    [Fact]
    public void AddAzureStorageServicesWithConnectionString_WithValidString_RegistersBlobServiceClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;EndpointSuffix=core.windows.net";

        // Act
        services.AddAzureStorageServicesWithConnectionString(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var client = provider.GetService<BlobServiceClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddAzureStorageServicesWithConnectionString_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            services.AddAzureStorageServicesWithConnectionString("")
        );
    }

    [Fact]
    public void AddAzureStorageServicesWithConnectionString_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;
        var connectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;EndpointSuffix=core.windows.net";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services!.AddAzureStorageServicesWithConnectionString(connectionString)
        );
    }
}
