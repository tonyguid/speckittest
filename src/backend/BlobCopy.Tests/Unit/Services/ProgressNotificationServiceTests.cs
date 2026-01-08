using BlobCopy.API.Hubs;
using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace BlobCopy.Tests.Unit.Services;

public class ProgressNotificationServiceTests
{
    private readonly Mock<IHubContext<BlobCopyHub, IBlobCopyClient>> _mockHubContext;
    private readonly Mock<ILogger<ProgressNotificationService>> _mockLogger;
    private readonly ProgressNotificationService _service;

    public ProgressNotificationServiceTests()
    {
        _mockHubContext = new Mock<IHubContext<BlobCopyHub, IBlobCopyClient>>();
        _mockLogger = new Mock<ILogger<ProgressNotificationService>>();
        _service = new ProgressNotificationService(_mockHubContext.Object, _mockLogger.Object);
    }

    // Constructor tests
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHubContextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProgressNotificationService(null, _mockLogger.Object)
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProgressNotificationService(_mockHubContext.Object, null)
        );
    }

    [Fact]
    public void Constructor_CreatesInstance_WhenPropertiesValid()
    {
        var service = new ProgressNotificationService(_mockHubContext.Object, _mockLogger.Object);
        Assert.NotNull(service);
    }

    // NotifyProgressAsync tests
    [Fact]
    public async Task NotifyProgressAsync_ThrowsArgumentException_WhenOperationIdIsNull()
    {
        var progress = new ProgressUpdate { BytesTransferred = 0, TotalBytes = 100 };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyProgressAsync(null, progress)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyProgressAsync_ThrowsArgumentException_WhenOperationIdIsEmpty()
    {
        var progress = new ProgressUpdate { BytesTransferred = 0, TotalBytes = 100 };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyProgressAsync("", progress)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyProgressAsync_ThrowsArgumentNullException_WhenProgressIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.NotifyProgressAsync("op-123", null)
        );
    }

    [Fact]
    public async Task NotifyProgressAsync_SendsProgressToGroup()
    {
        var operationId = "op-123";
        var progress = new ProgressUpdate 
        { 
            BytesTransferred = 1024,
            TotalBytes = 2048,
            TimestampUtc = DateTime.UtcNow
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyProgressAsync(operationId, progress);

        mockClients.Verify(x => x.Group(operationId), Times.Once);
    }

    [Fact]
    public async Task NotifyProgressAsync_LogsProgressInfo()
    {
        var operationId = "op-123";
        var progress = new ProgressUpdate
        {
            BytesTransferred = 1024,
            TotalBytes = 2048
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyProgressAsync(operationId, progress);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }

    // NotifyCompletionAsync tests
    [Fact]
    public async Task NotifyCompletionAsync_ThrowsArgumentException_WhenOperationIdIsNull()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyCompletionAsync(null, operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyCompletionAsync_ThrowsArgumentException_WhenOperationIdIsEmpty()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyCompletionAsync("", operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyCompletionAsync_ThrowsArgumentNullException_WhenOperationIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.NotifyCompletionAsync("op-123", null)
        );
    }

    [Fact]
    public async Task NotifyCompletionAsync_SendsCompletionToGroup()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Completed,
            TotalBytes = 2048,
            BytesTransferred = 2048,
            StartedAt = DateTime.UtcNow.AddSeconds(-10),
            CompletedAt = DateTime.UtcNow
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyCompletionAsync(operationId, operation);

        mockClients.Verify(x => x.Group(operationId), Times.Once);
    }

    [Fact]
    public async Task NotifyCompletionAsync_LogsCompletionInfo()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Completed,
            TotalBytes = 2048,
            StartedAt = DateTime.UtcNow.AddSeconds(-10),
            CompletedAt = DateTime.UtcNow
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyCompletionAsync(operationId, operation);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }

    // NotifyFailureAsync tests
    [Fact]
    public async Task NotifyFailureAsync_ThrowsArgumentException_WhenOperationIdIsNull()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyFailureAsync(null, operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyFailureAsync_ThrowsArgumentException_WhenOperationIdIsEmpty()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyFailureAsync("", operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyFailureAsync_ThrowsArgumentNullException_WhenOperationIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.NotifyFailureAsync("op-123", null)
        );
    }

    [Fact]
    public async Task NotifyFailureAsync_SendsFailureToGroup()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Failed,
            Errors = new List<ValidationError>
            {
                new ValidationError { Message = "Blob not found", Code = "NOT_FOUND" }
            }
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyFailureAsync(operationId, operation);

        mockClients.Verify(x => x.Group(operationId), Times.Once);
    }

    [Fact]
    public async Task NotifyFailureAsync_LogsErrorInfo()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Failed,
            Errors = new List<ValidationError>
            {
                new ValidationError { Message = "Access denied", Code = "FORBIDDEN" }
            }
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyFailureAsync(operationId, operation);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }

    // NotifyCancellationAsync tests
    [Fact]
    public async Task NotifyCancellationAsync_ThrowsArgumentException_WhenOperationIdIsNull()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyCancellationAsync(null, operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyCancellationAsync_ThrowsArgumentException_WhenOperationIdIsEmpty()
    {
        var operation = new BlobCopyOperation { Id = "op-123" };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyCancellationAsync("", operation)
        );
        Assert.Equal("operationId", ex.ParamName);
    }

    [Fact]
    public async Task NotifyCancellationAsync_ThrowsArgumentNullException_WhenOperationIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.NotifyCancellationAsync("op-123", null)
        );
    }

    [Fact]
    public async Task NotifyCancellationAsync_SendsCancellationToGroup()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            BytesTransferred = 1024,
            TotalBytes = 2048
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyCancellationAsync(operationId, operation);

        mockClients.Verify(x => x.Group(operationId), Times.Once);
    }

    [Fact]
    public async Task NotifyCancellationAsync_LogsCancellationInfo()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            BytesTransferred = 1024,
            TotalBytes = 2048
        };

        var mockClients = new Mock<IHubClients<IBlobCopyClient>>();
        var mockGroupClients = new Mock<IBlobCopyClient>();

        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(operationId)).Returns(mockGroupClients.Object);

        await _service.NotifyCancellationAsync(operationId, operation);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }

    // Whitespace handling
    [Fact]
    public async Task NotifyProgressAsync_TreatsWhitespaceAsEmpty()
    {
        var progress = new ProgressUpdate { BytesTransferred = 0, TotalBytes = 100 };
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyProgressAsync("   ", progress)
        );
        Assert.Equal("operationId", ex.ParamName);
    }
}
