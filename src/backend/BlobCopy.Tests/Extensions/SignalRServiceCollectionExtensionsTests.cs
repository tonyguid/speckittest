using Xunit;
using Microsoft.Extensions.DependencyInjection;
using BlobCopy.API.Extensions;

namespace BlobCopy.Tests.Extensions;

/// <summary>
/// Unit tests for SignalRServiceCollectionExtensions.
/// </summary>
public class SignalRServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlobCopySignalR_RegistersSignalRServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBlobCopySignalR();
        var provider = services.BuildServiceProvider();

        // Assert
        var signalRRegistered = services.Any(sd => 
            sd.ServiceType.Name.Contains("HubProtocolResolver") || 
            sd.ServiceType.Name.Contains("HubDispatcher")
        );
        Assert.True(signalRRegistered, "SignalR services should be registered");
    }

    [Fact]
    public void AddBlobCopySignalR_WithNullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services!.AddBlobCopySignalR()
        );
    }

    [Fact]
    public void AddBlobCopySignalR_CanBeChained()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlobCopySignalR();

        // Assert
        Assert.NotNull(result);
        Assert.Same(services, result);
    }
}
