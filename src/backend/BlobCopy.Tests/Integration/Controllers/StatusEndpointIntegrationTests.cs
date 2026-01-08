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
/// Integration tests for the /status/{id} endpoint.
/// Tests status retrieval at various operation states.
/// </summary>
public class StatusEndpointIntegrationTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public StatusEndpointIntegrationTests()
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
    public void GetStatus_WithPendingOperation_ReturnsOkWithPendingStatus()
    {
        var operationId = "op-pending-001";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Pending,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-5),
            TotalBytes = 1024 * 1024 * 100,
            BytesTransferred = 0
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Pending, returnedOp.Status);
        Assert.Equal(0, returnedOp.BytesTransferred);
    }

    [Fact]
    public void GetStatus_WithRunningOperation_ReturnsProgressData()
    {
        var operationId = "op-running-001";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.InProgress,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-60),
            TotalBytes = 1024 * 1024 * 100,
            BytesTransferred = 1024 * 1024 * 50 // 50% complete
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.InProgress, returnedOp.Status);
        Assert.Equal(1024 * 1024 * 50, returnedOp.BytesTransferred);
    }

    [Fact]
    public void GetStatus_WithCompletedOperation_ReturnsCompletionDetails()
    {
        var operationId = "op-completed-001";
        var startTime = DateTime.UtcNow.AddSeconds(-120);
        var endTime = DateTime.UtcNow;
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Completed,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = startTime,
            CompletedAt = endTime,
            TotalBytes = 1024 * 1024 * 100,
            BytesTransferred = 1024 * 1024 * 100,
            DurationSeconds = 120
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Completed, returnedOp.Status);
        Assert.NotNull(returnedOp.CompletedAt);
        Assert.Equal(120, returnedOp.DurationSeconds);
    }

    [Fact]
    public void GetStatus_WithFailedOperation_ReturnsErrorDetails()
    {
        var operationId = "op-failed-001";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Failed,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-30),
            CompletedAt = DateTime.UtcNow,
            BytesTransferred = 1024 * 512,
            TotalBytes = 1024 * 1024 * 100,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "operation",
                    Message = "Access denied to destination container",
                    Code = "AUTHENTICATION_FAILED"
                }
            }
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Failed, returnedOp.Status);
        Assert.NotEmpty(returnedOp.Errors);
        Assert.Equal("AUTHENTICATION_FAILED", returnedOp.Errors[0].Code);
    }

    [Fact]
    public void GetStatus_WithCancelledOperation_ReturnsCancelledStatus()
    {
        var operationId = "op-cancelled-001";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-45),
            CompletedAt = DateTime.UtcNow.AddSeconds(-10),
            BytesTransferred = 1024 * 1024 * 75,
            TotalBytes = 1024 * 1024 * 100
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(BlobCopyStatus.Cancelled, returnedOp.Status);
    }

    [Fact]
    public void GetStatus_WithNonExistentOperation_ReturnsNotFound()
    {
        var operationId = "nonexistent-op";

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns((BlobCopyOperation)null);

        var result = _controller.GetStatus(operationId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public void GetStatus_WithEmptyId_ReturnsBadRequest()
    {
        var result = _controller.GetStatus("");

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public void GetStatus_CallsCopyServiceWithCorrectId()
    {
        var operationId = "op-123";
        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(new BlobCopyOperation { Id = operationId });

        _controller.GetStatus(operationId);

        _mockCopyService.Verify(x => x.GetOperationStatus(operationId), Times.Once);
    }

    [Fact]
    public void GetStatus_PreservesAllOperationProperties()
    {
        var operationId = "op-props-001";
        var sourceUri = "https://storage.blob.core.windows.net/source/file.vhd";
        var destUri = "https://storage.blob.core.windows.net/dest/file.vhd";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.InProgress,
            SourceUri = sourceUri,
            DestinationUri = destUri,
            SourceBlobName = "file.vhd",
            StartedAt = DateTime.UtcNow.AddSeconds(-30),
            TotalBytes = 1024 * 1024 * 100,
            BytesTransferred = 1024 * 1024 * 50,
            Errors = new List<ValidationError>()
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        
        Assert.Equal(operationId, returnedOp.Id);
        Assert.Equal(sourceUri, returnedOp.SourceUri);
        Assert.Equal(destUri, returnedOp.DestinationUri);
        Assert.Equal(BlobCopyStatus.InProgress, returnedOp.Status);
        Assert.Equal(1024 * 1024 * 50, returnedOp.BytesTransferred);
    }

    [Theory]
    [InlineData(BlobCopyStatus.Pending)]
    [InlineData(BlobCopyStatus.InProgress)]
    [InlineData(BlobCopyStatus.Completed)]
    [InlineData(BlobCopyStatus.Failed)]
    [InlineData(BlobCopyStatus.Cancelled)]
    public void GetStatus_SupportsAllOperationStates(BlobCopyStatus status)
    {
        var operationId = $"op-{status}-001";
        var operation = new BlobCopyOperation
        {
            Id = operationId,
            Status = status,
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOp = Assert.IsType<BlobCopyOperation>(okResult.Value);
        Assert.Equal(status, returnedOp.Status);
    }

    [Fact]
    public void GetStatus_HandlesCopyServiceException()
    {
        _mockCopyService
            .Setup(x => x.GetOperationStatus(It.IsAny<string>()))
            .Throws(new Exception("Database connection failed"));

        var result = _controller.GetStatus("op-123");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public void GetStatus_ReturnsNotFoundErrorResponse_WhenOperationMissing()
    {
        var operationId = "missing-op-123";

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns((BlobCopyOperation)null);

        var result = _controller.GetStatus(operationId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", errorResponse.Code);
    }
}
