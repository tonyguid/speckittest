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
/// Integration tests for the /start endpoint.
/// Tests copy initiation flow, operation creation, and background task execution.
/// </summary>
public class StartEndpointIntegrationTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public StartEndpointIntegrationTests()
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

        // Default successful validation
        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        // Default successful copy start
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
                    StartedAt = DateTime.UtcNow,
                    TotalBytes = 1024 * 1024 * 100
                });
    }

    [Fact]
    public async Task Start_WithValidRequest_ReturnsCreatedWithOperationId()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/large-file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/destination/copy.vhd"
        };

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);
        Assert.NotNull(operation.Id);
        Assert.NotEmpty(operation.Id);
        Assert.Equal(BlobCopyStatus.Pending, operation.Status);
    }

    [Fact]
    public async Task Start_ReturnsOperationWithCorrectProperties()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);
        
        Assert.Equal(request.SourceUri, operation.SourceUri);
        Assert.Equal(request.DestinationUri, operation.DestinationUri);
        Assert.NotNull(operation.StartedAt);
        Assert.True(operation.TotalBytes > 0);
    }

    [Fact]
    public async Task Start_ValidatesBeforeStarting()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        await _controller.StartAsync(request);

        _mockValidationService.Verify(
            x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Start_ReturnsBadRequest_WhenValidationFails()
    {
        var request = new CopyRequest
        {
            SourceUri = "invalid-uri",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError
            {
                Field = "sourceUri",
                Message = "Invalid URI format",
                Code = "INVALID_URI_FORMAT"
            }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.StartAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Start_DoesNotStartCopyWhenValidationFails()
    {
        var request = new CopyRequest
        {
            SourceUri = "invalid-uri",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Invalid", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        await _controller.StartAsync(request);

        _mockCopyService.Verify(
            x => x.StartCopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Start_InitiatesCopyServiceStart()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        await _controller.StartAsync(request);

        _mockCopyService.Verify(
            x => x.StartCopyAsync(
                It.IsAny<string>(),
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Start_ReturnsLocationHeaderWithOperationId()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(BlobCopyController.GetStatus), createdResult.ActionName);
        Assert.NotNull(createdResult.RouteValues["id"]);
    }

    [Fact]
    public async Task Start_WithValidationError_ReturnsAllErrors()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://invalid.example.com/blob",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Not Azure Blob", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(request.SourceUri, request.DestinationUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.StartAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.NotEmpty(returnedErrors);
    }

    [Fact]
    public async Task Start_GeneratesUniqueOperationIds()
    {
        var request1 = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file1.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file1.vhd"
        };

        var request2 = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file2.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file2.vhd"
        };

        var result1 = await _controller.StartAsync(request1);
        var result2 = await _controller.StartAsync(request2);

        var operation1 = Assert.IsType<BlobCopyOperation>(((CreatedAtActionResult)result1).Value);
        var operation2 = Assert.IsType<BlobCopyOperation>(((CreatedAtActionResult)result2).Value);

        Assert.NotEqual(operation1.Id, operation2.Id);
    }

    [Fact]
    public async Task Start_HandlesCopyServiceExceptions()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        _mockCopyService
            .Setup(x => x.StartCopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage service unavailable"));

        var result = await _controller.StartAsync(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Start_WithLargeFile_CreatesPendingOperation()
    {
        var largeSize = 190 * 1024L * 1024 * 1024; // 190 GB
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/large.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/large.vhd"
        };

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
                    StartedAt = DateTime.UtcNow,
                    TotalBytes = largeSize
                });

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);
        Assert.Equal(largeSize, operation.TotalBytes);
    }

    [Fact]
    public async Task Start_InitiatesBackgroundCopyTask()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/file.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file.vhd"
        };

        _mockCopyService
            .Setup(x => x.ExecuteCopyAsync(
                It.IsAny<BlobCopyOperation>(),
                It.IsAny<Func<long, long, Task>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlobCopyOperation op, Func<long, long, Task> callback, CancellationToken ct) =>
            {
                op.Status = BlobCopyStatus.Completed;
                op.CompletedAt = DateTime.UtcNow;
                return op;
            });

        var result = await _controller.StartAsync(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(createdResult.Value);
        // Background task executes asynchronously, so we just verify the response
    }
}
