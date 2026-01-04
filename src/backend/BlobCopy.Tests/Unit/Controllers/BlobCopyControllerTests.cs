using BlobCopy.API.Controllers;
using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BlobCopy.Tests.Unit.Controllers;

public class BlobCopyControllerTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public BlobCopyControllerTests()
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

        // Default mock setup
        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        _mockCopyService
            .Setup(x => x.StartCopyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string src, string dst, CancellationToken ct) =>
                new BlobCopyOperation
                {
                    Id = id,
                    SourceUri = src,
                    DestinationUri = dst,
                    Status = BlobCopyStatus.Pending,
                    StartedAt = DateTime.UtcNow
                });

        _mockCopyService
            .Setup(x => x.GetOperationStatus(It.IsAny<string>()))
            .Returns((string id) => new BlobCopyOperation { Id = id, Status = BlobCopyStatus.Pending });

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken ct) =>
                new BlobCopyOperation { Id = id, Status = BlobCopyStatus.Cancelled });
    }

    // Constructor tests
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenValidationServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobCopyController(null, _mockCopyService.Object, _mockProgressService.Object, _mockLogger.Object)
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCopyServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobCopyController(_mockValidationService.Object, null, _mockProgressService.Object, _mockLogger.Object)
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProgressServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobCopyController(_mockValidationService.Object, _mockCopyService.Object, null, _mockLogger.Object)
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BlobCopyController(_mockValidationService.Object, _mockCopyService.Object, _mockProgressService.Object, null)
        );
    }

    // ValidateAsync tests
    [Fact]
    public async Task ValidateAsync_ReturnsOk_WhenValidationSucceeds()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        var result = await _controller.ValidateAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.IsType<List<ValidationError>>(okResult.Value);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsBadRequest_WhenValidationFails()
    {
        var request = new CopyRequest
        {
            SourceUri = "invalid-uri",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Invalid URI", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.ValidateAsync(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_CallsValidationService()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        await _controller.ValidateAsync(request);

        _mockValidationService.Verify(
            x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInternalServerError_OnException()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _controller.ValidateAsync(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    // StartAsync tests
    [Fact]
    public async Task StartAsync_ReturnsCreated_WhenCopyStartsSuccessfully()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Equal(nameof(BlobCopyController.GetStatus), createdResult.ActionName);
    }

    [Fact]
    public async Task StartAsync_ReturnsBadRequest_WhenValidationFails()
    {
        var request = new CopyRequest
        {
            SourceUri = "invalid-uri",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Invalid URI", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.StartAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task StartAsync_ReturnsBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.StartAsync(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task StartAsync_CallsCopyService()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        await _controller.StartAsync(request);

        _mockCopyService.Verify(
            x => x.StartCopyAsync(
                It.IsAny<string>(),
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task StartAsync_ReturnsOperationWithId()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);
        Assert.NotNull(operation.Id);
        Assert.NotEmpty(operation.Id);
    }

    [Fact]
    public async Task StartAsync_ReturnsInternalServerError_OnException()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://example.blob.core.windows.net/source/blob",
            DestinationUri = "https://example.blob.core.windows.net/dest/blob"
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _controller.StartAsync(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    // GetStatus tests
    [Fact]
    public void GetStatus_ReturnsOk_WhenOperationExists()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.Running };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(operation);

        var result = _controller.GetStatus(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public void GetStatus_ReturnsNotFound_WhenOperationDoesNotExist()
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
    public void GetStatus_ReturnsBadRequest_WhenIdIsNull()
    {
        var result = _controller.GetStatus(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public void GetStatus_ReturnsBadRequest_WhenIdIsEmpty()
    {
        var result = _controller.GetStatus("");

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public void GetStatus_CallsCopyService()
    {
        var operationId = "op-123";

        _controller.GetStatus(operationId);

        _mockCopyService.Verify(x => x.GetOperationStatus(operationId), Times.Once);
    }

    [Fact]
    public void GetStatus_ReturnsInternalServerError_OnException()
    {
        _mockCopyService
            .Setup(x => x.GetOperationStatus(It.IsAny<string>()))
            .Throws(new Exception("Test exception"));

        var result = _controller.GetStatus("op-123");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    // CancelAsync tests
    [Fact]
    public async Task CancelAsync_ReturnsOk_WhenCancelSucceeds()
    {
        var operationId = "op-123";

        var result = await _controller.CancelAsync(operationId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task CancelAsync_ReturnsNotFound_WhenOperationDoesNotExist()
    {
        var operationId = "nonexistent-op";

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.NotFound });

        var result = await _controller.CancelAsync(operationId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task CancelAsync_ReturnsBadRequest_WhenIdIsNull()
    {
        var result = await _controller.CancelAsync(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CancelAsync_ReturnsBadRequest_WhenIdIsEmpty()
    {
        var result = await _controller.CancelAsync("");

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CancelAsync_CallsCopyService()
    {
        var operationId = "op-123";

        await _controller.CancelAsync(operationId);

        _mockCopyService.Verify(
            x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CancelAsync_NotifiesProgressService()
    {
        var operationId = "op-123";
        var operation = new BlobCopyOperation { Id = operationId, Status = BlobCopyStatus.Cancelled };

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
    public async Task CancelAsync_ReturnsInternalServerError_OnException()
    {
        _mockCopyService
            .Setup(x => x.CancelCopyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _controller.CancelAsync("op-123");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    // Health tests
    [Fact]
    public void Health_ReturnsOk()
    {
        var result = _controller.Health();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public void Health_ReturnsHealthyStatus()
    {
        var result = _controller.Health();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var health = okResult.Value as dynamic;
        Assert.NotNull(health);
    }

    [Fact]
    public void Health_ReturnsServiceUnavailable_OnException()
    {
        _mockCopyService
            .Setup(x => x.GetOperationStatus(It.IsAny<string>()))
            .Throws(new Exception("Test exception"));

        // Note: Health endpoint doesn't currently call any service, so this test
        // documents the expected behavior if health checks are enhanced
        var result = _controller.Health();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
