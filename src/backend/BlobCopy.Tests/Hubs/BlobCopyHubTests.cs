using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using BlobCopy.API.Hubs;
using BlobCopy.API.Models;
using Microsoft.Extensions.Logging;

namespace BlobCopy.Tests.Hubs;

/// <summary>
/// Unit tests for BlobCopyHub SignalR hub.
/// Tests hub lifecycle, client subscriptions, and message broadcasting.
/// </summary>
public class BlobCopyHubTests : IDisposable
{
    private readonly Mock<ILogger<BlobCopyHub>> _mockLogger;
    private readonly Mock<IHubCallerClients<IBlobCopyClient>> _mockClients;
    private readonly Mock<IBlobCopyClient> _mockCaller;
    private readonly Mock<IGroupManager> _mockGroupManager;
    private readonly HubCallerContext _hubContext;

    public BlobCopyHubTests()
    {
        _mockLogger = new Mock<ILogger<BlobCopyHub>>();
        _mockClients = new Mock<IHubCallerClients<IBlobCopyClient>>();
        _mockCaller = new Mock<IBlobCopyClient>();
        _mockGroupManager = new Mock<IGroupManager>();

        // Setup hub context
        var mockHubContext = new Mock<HubCallerContext>();
        mockHubContext.Setup(h => h.ConnectionId).Returns("test-connection-id");
        _hubContext = mockHubContext.Object;

        // Setup clients mock to return caller when requested
        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);
    }

    private BlobCopyHub CreateHub(string? connectionId = null)
    {
        var hub = new BlobCopyHub(_mockLogger.Object);
        
        // Setup context
        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(h => h.ConnectionId).Returns(connectionId ?? "test-connection-id");
        
        hub.Context = mockContext.Object;
        hub.Clients = _mockClients.Object;
        hub.Groups = _mockGroupManager.Object;
        
        return hub;
    }

    public void Dispose()
    {
        // Reset static state in the hub by creating an instance and clearing connections
        var hub = CreateHub();
        // Access the private static field via reflection to reset it
        var hubType = typeof(BlobCopyHub);
        var connectionsField = hubType.GetField("_operationConnections", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (connectionsField != null)
        {
            var connections = connectionsField.GetValue(null) as System.Collections.IDictionary;
            connections?.Clear();
        }
    }

    [Fact]
    public async Task OnConnectedAsync_SendsConnectionEstablishedMessage()
    {
        // Arrange
        var hub = CreateHub();
        var tcs = new TaskCompletionSource<bool>();
        
        _mockCaller
            .Setup(c => c.ConnectionEstablished(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        _mockCaller.Verify(
            c => c.ConnectionEstablished(
                It.Is<string>(s => s == "Connected to Blob Copy Hub"),
                It.IsAny<DateTime>(),
                It.Is<string>(s => s == "test-connection-id")
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task JoinCopyOperation_AddsConnectionToGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-123";

        _mockGroupManager
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.JoinCopyOperation(operationId);

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(
                "test-connection-id",
                $"operation-{operationId}",
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task JoinCopyOperation_WithEmptyId_DoesNothing()
    {
        // Arrange
        var hub = CreateHub();

        _mockGroupManager
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.JoinCopyOperation("");

        // Assert
        _mockGroupManager.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task LeaveCopyOperation_RemovesConnectionFromGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-456";

        _mockGroupManager
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.LeaveCopyOperation(operationId);

        // Assert
        _mockGroupManager.Verify(
            g => g.RemoveFromGroupAsync(
                "test-connection-id",
                $"operation-{operationId}",
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task BroadcastProgressUpdate_SendsUpdateToGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-789";
        var update = new ProgressUpdate
        {
            CopyOperationId = operationId,
            BytesTransferred = 1000,
            TotalBytes = 2000,
            ProgressPercentage = 50m
        };

        var mockGroupClients = new Mock<IBlobCopyClient>();
        _mockClients.Setup(c => c.Group($"operation-{operationId}")).Returns(mockGroupClients.Object);

        mockGroupClients
            .Setup(g => g.ProgressUpdate(It.IsAny<ProgressUpdate>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.BroadcastProgressUpdate(operationId, update);

        // Assert
        _mockClients.Verify(c => c.Group($"operation-{operationId}"), Times.Once);
    }

    [Fact]
    public async Task BroadcastCopyCompleted_SendsCompletionToGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-completed";
        var mockGroupClients = new Mock<IBlobCopyClient>();
        
        _mockClients.Setup(c => c.Group($"operation-{operationId}")).Returns(mockGroupClients.Object);

        mockGroupClients
            .Setup(g => g.CopyCompleted(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.BroadcastCopyCompleted(
            operationId,
            "https://dest.blob.core.windows.net/container/blob",
            1024,
            30,
            DateTime.UtcNow
        );

        // Assert
        _mockClients.Verify(c => c.Group($"operation-{operationId}"), Times.Once);
    }

    [Fact]
    public async Task BroadcastCopyFailed_SendsErrorToGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-failed";
        var mockGroupClients = new Mock<IBlobCopyClient>();
        
        _mockClients.Setup(c => c.Group($"operation-{operationId}")).Returns(mockGroupClients.Object);

        mockGroupClients
            .Setup(g => g.CopyFailed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.BroadcastCopyFailed(
            operationId,
            "SOURCE_NOT_FOUND",
            "Source blob not found",
            false,
            DateTime.UtcNow
        );

        // Assert
        _mockClients.Verify(c => c.Group($"operation-{operationId}"), Times.Once);
    }

    [Fact]
    public async Task BroadcastOperationCancelled_SendsCancellationToGroup()
    {
        // Arrange
        var hub = CreateHub();
        var operationId = "op-cancelled";
        var mockGroupClients = new Mock<IBlobCopyClient>();
        
        _mockClients.Setup(c => c.Group($"operation-{operationId}")).Returns(mockGroupClients.Object);

        mockGroupClients
            .Setup(g => g.OperationCancelled(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await hub.BroadcastOperationCancelled(
            operationId,
            500,
            DateTime.UtcNow,
            "Operation cancelled by user"
        );

        // Assert
        _mockClients.Verify(c => c.Group($"operation-{operationId}"), Times.Once);
    }

    [Fact]
    public async Task BroadcastProgressUpdate_WithNullData_DoesNotBroadcast()
    {
        // Arrange
        var hub = CreateHub();

        // Act
        await hub.BroadcastProgressUpdate("op-123", null!);

        // Assert
        _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastProgressUpdate_WithEmptyOperationId_DoesNotBroadcast()
    {
        // Arrange
        var hub = CreateHub();
        var update = new ProgressUpdate();

        // Act
        await hub.BroadcastProgressUpdate("", update);

        // Assert
        _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetConnectedClientsForOperation_ReturnsZeroForNonExistentOperation()
    {
        // Arrange
        var hub = CreateHub();

        // Act
        var count = hub.GetConnectedClientsForOperation("non-existent");

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetTotalConnectedClients_ReturnsZeroInitially()
    {
        // Arrange
        var hub = CreateHub();

        // Act
        var count = hub.GetTotalConnectedClients();

        // Assert
        Assert.Equal(0, count);
    }
}
