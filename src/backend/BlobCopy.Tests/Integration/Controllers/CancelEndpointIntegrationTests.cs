using BlobCopy.API.Controllers;
using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace BlobCopy.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for the /cancel/{id} endpoint.
/// Tests cancellation flow and state transitions.
/// </summary>
public class CancelEndpointIntegrationTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public CancelEndpointIntegrationTests()
    {
        _mockValidationService = new Mock<IBlobValidationService>();
        _mockCopyService = new Mock<IBlobCopyService>();
        _mockProgressService = new Mock<IProgressNotificationService>();
        _mockLogger = new Mock<ILogger<BlobCopyController>>();

        _controller = new BlobCopyController(
            _mockValidationService.Object,
            _mockCopyService.Object,
            _mockProgressService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Cancel_WithPendingOperation_ReturnsCancelledStatus()
    {
        var operationId = "op-pending-cancel";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-5),
            CompletedAt = DateTime.UtcNow,
            BytesTransferred = 0,
            TotalBytes = 1024 * 1024 * 100
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var cancelledOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Cancelled, cancelledOp.Status);
    }

    [Fact]
    public async Task Cancel_WithRunningOperation_ReturnsCancelledStatus()
    {
        var operationId = "op-running-cancel";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-60),
            CompletedAt = DateTime.UtcNow,
            BytesTransferred = 1024 * 1024 * 50,
            TotalBytes = 1024 * 1024 * 100
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var cancelledOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Cancelled, cancelledOp.Status);
        Assert.NotNull(cancelledOp.CompletedAt);
    }

    [Fact]
    public async Task Cancel_WithCompletedOperation_KeepsCompletedStatus()
    {
        var operationId = "op-completed-cancel";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Completed,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-120),
            CompletedAt = DateTime.UtcNow.AddSeconds(-30),
            BytesTransferred = 1024 * 1024 * 100,
            TotalBytes = 1024 * 1024 * 100,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "operation",
                    Message = "Cannot cancel a completed operation",
                    Code = "INVALID_STATE_TRANSITION"
                }
            }
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var resultOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Completed, resultOp.Status);
        Assert.NotEmpty(resultOp.Errors);
    }

    [Fact]
    public async Task Cancel_WithNonExistentOperation_ReturnsNotFound()
    {
        var operationId = "nonexistent-op-cancel";

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.NotFound });

        var result = await _controller.CancelAsync(operationId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithEmptyId_ReturnsBadRequest()
    {
        var result = await _controller.CancelAsync("");

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithNullId_ReturnsBadRequest()
    {
        var result = await _controller.CancelAsync(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_CallsCopyService()
    {
        var operationId = "op-cancel-service-call";
        var operation = new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.Cancelled };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        await _controller.CancelAsync(operationId);

        _mockCopyService.Verify(
            x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Cancel_NotifiesProgressService()
    {
        var operationId = "op-cancel-notify";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            BytesTransferred = 1024 * 512,
            TotalBytes = 1024 * 1024 * 100
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        await _controller.CancelAsync(operationId);

        _mockProgressService.Verify(
            x => x.NotifyCancellationAsync(operationId, operation, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Cancel_WithPartialProgress_PreservesProgress()
    {
        var operationId = "op-partial-cancel";
        var BytesTransferred = 1024 * 1024 * 75;
        var totalBytes = 1024 * 1024 * 100;
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-45),
            CompletedAt = DateTime.UtcNow,
            BytesTransferred = BytesTransferred,
            TotalBytes = totalBytes
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var cancelledOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BytesTransferred, cancelledOp.BytesTransferred);
        Assert.Equal(totalBytes, cancelledOp.TotalBytes);
    }

    [Fact]
    public async Task Cancel_HandlesCopyServiceException()
    {
        var operationId = "op-cancel-exception";

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage service error"));

        var result = await _controller.CancelAsync(operationId);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_HandlesProgressServiceException()
    {
        var operationId = "op-progress-notify-exception";
        var operation = new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.Cancelled };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        _mockProgressService
            .Setup(x => x.NotifyCancellationAsync(operationId, operation, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR notification failed"));

        var result = await _controller.CancelAsync(operationId);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_ReturnsCorrectStatusCode_ForSuccess()
    {
        var operationId = "op-cancel-status-code";
        var operation = new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.Cancelled };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithFailedOperation_KeepsFailedStatus()
    {
        var operationId = "op-failed-cancel";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Failed,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            Errors = new List<ValidationError>
            {
                new ValidationError { Message = "Access denied", Code = "AUTHENTICATION_FAILED" }
            }
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var resultOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Failed, resultOp.Status);
    }

    [Fact]
    public async Task Cancel_OperationReturnsCompletedAtTime()
    {
        var operationId = "op-cancel-timestamp";
        var completedAt = DateTime.UtcNow;
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            CompletedAt = completedAt
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(operation);

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var cancelledOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.NotNull(cancelledOp.CompletedAt);
    }
}
