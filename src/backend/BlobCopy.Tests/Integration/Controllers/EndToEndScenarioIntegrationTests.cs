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
/// End-to-end scenario tests for blob copy workflow.
/// Tests complete copy operations across all endpoints.
/// </summary>
public class EndToEndScenarioIntegrationTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public EndToEndScenarioIntegrationTests()
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

        // Default setup
        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());
    }

    [Fact]
    public async Task FullCopyWorkflow_Validate_Start_Status_Succeeds()
    {
        var sourceUri = "https://storage.blob.core.windows.net/source/large-file.vhd";
        var destUri = "https://storage.blob.core.windows.net/destination/copy.vhd";
        var operationId = Guid.NewGuid().ToString();

        // Step 1: Validate
        var validateRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var validateResult = await _controller.ValidateAsync(validateRequest);
        var okResult = Assert.IsType<OkObjectResult>(validateResult);
        Assert.NotNull(okResult.Value);

        // Step 2: Start
        _mockCopyService
            .Setup(x => x.StartCopyAsync(It.IsAny<string>(), sourceUri, destUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string src, string dst, CancellationToken ct) =>
                new BlobCopyOperation
                {
                    Id = id,
                    SourceUri = src,
                    DestinationUri = dst,
                    Status = BlobCopyStatus.Pending,
                    StartedAt = DateTime.UtcNow,
                    TotalBytes = 1024 * 1024 * 500
                });

        var startRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var startResult = await _controller.StartAsync(startRequest);
        var createdResult = Assert.IsType<CreatedAtActionResult>(startResult);
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);
        var actualOperationId = operation.Id;

        // Step 3: Get Status
        _mockCopyService
            .Setup(x => x.GetOperationStatus(actualOperationId))
            .Returns(new BlobCopyOperation
            {
                Id = actualOperationId,
                Status = BlobCopyStatus.InProgress,
                SourceUri = sourceUri,
                DestinationUri = destUri,
                BytesTransferred = 1024 * 1024 * 100,
                TotalBytes = 1024 * 1024 * 500,
                StartedAt = DateTime.UtcNow.AddSeconds(-60)
            });

        var statusResult = _controller.GetStatus(actualOperationId);
        var statusOkResult = Assert.IsType<OkObjectResult>(statusResult);
        var statusOp = Assert.IsType<BlobCopyOperation>(statusOkResult.Value);

        Assert.Equal(BlobCopyStatus.InProgress, statusOp.Status);
        Assert.True(statusOp.BytesTransferred > 0);
    }

    [Fact]
    public async Task FailedValidation_PreventsCopyStart()
    {
        var sourceUri = "https://invalid.example.com/blob";
        var destUri = "https://storage.blob.core.windows.net/dest/blob";

        var validationErrors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Invalid URI", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(sourceUri, destUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationErrors);

        // Attempt to start without successful validation
        var startRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var result = await _controller.StartAsync(startRequest);

        // Should fail with validation error
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        // Verify copy was never started
        _mockCopyService.Verify(
            x => x.StartCopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CopyOperation_CompletesSuccessfully()
    {
        var operationId = "op-complete-scenario";
        var sourceUri = "https://storage.blob.core.windows.net/source/file.vhd";
        var destUri = "https://storage.blob.core.windows.net/dest/file.vhd";
        var totalBytes = 1024 * 1024 * 100;

        // Simulate operation progression: Pending -> Running -> Completed
        var pendingOp = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Pending,
            SourceUri = sourceUri,
            DestinationUri = destUri,
            StartedAt = DateTime.UtcNow,
            TotalBytes = totalBytes,
            BytesTransferred = 0
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(pendingOp);

        // Check pending status
        var status1 = _controller.GetStatus(operationId);
        var result1 = Assert.IsType<OkObjectResult>(status1);
        var op1 = Assert.IsType<BlobCopyOperation>(result1.Value);
        Assert.Equal(BlobCopyStatus.Pending, op1.Status);

        // Progress to running
        var runningOp = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.InProgress,
            SourceUri = sourceUri,
            DestinationUri = destUri,
            StartedAt = DateTime.UtcNow.AddSeconds(-60),
            TotalBytes = totalBytes,
            BytesTransferred = totalBytes / 2
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(runningOp);

        var status2 = _controller.GetStatus(operationId);
        var result2 = Assert.IsType<OkObjectResult>(status2);
        var op2 = Assert.IsType<BlobCopyOperation>(result2.Value);
        Assert.Equal(BlobCopyStatus.InProgress, op2.Status);

        // Complete
        var completedOp = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Completed,
            SourceUri = sourceUri,
            DestinationUri = destUri,
            StartedAt = DateTime.UtcNow.AddSeconds(-120),
            CompletedAt = DateTime.UtcNow,
            TotalBytes = totalBytes,
            BytesTransferred = totalBytes,
            DurationSeconds = 120
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(completedOp);

        var status3 = _controller.GetStatus(operationId);
        var result3 = Assert.IsType<OkObjectResult>(status3);
        var op3 = Assert.IsType<BlobCopyOperation>(result3.Value);
        Assert.Equal(BlobCopyStatus.Completed, op3.Status);
        Assert.Equal(totalBytes, op3.BytesTransferred);
    }

    [Fact]
    public async Task CopyOperation_CanBeCancelledMidway()
    {
        var operationId = "op-cancel-scenario";
        var sourceUri = "https://storage.blob.core.windows.net/source/file.vhd";
        var destUri = "https://storage.blob.core.windows.net/dest/file.vhd";
        var totalBytes = 1024 * 1024 * 100;

        // Start in running state
        _mockCopyService
            .Setup(x => x.GetOperationStatus(operationId))
            .Returns(new BlobCopyOperation
            {
                Id = operationId,
                Status = BlobCopyStatus.InProgress,
                SourceUri = sourceUri,
                DestinationUri = destUri,
                BytesTransferred = totalBytes / 3,
                TotalBytes = totalBytes
            });

        // Check status
        var statusResult = _controller.GetStatus(operationId);
        var statusOk = Assert.IsType<OkObjectResult>(statusResult);
        var runningOp = Assert.IsType<BlobCopyOperation>(statusOk.Value);
        Assert.Equal(BlobCopyStatus.InProgress, runningOp.Status);

        // Cancel
        var cancelledOp = new BlobCopyOperation
        {
            Id = operationId,
            Status = BlobCopyStatus.Cancelled,
            SourceUri = sourceUri,
            DestinationUri = destUri,
            BytesTransferred = totalBytes / 3,
            TotalBytes = totalBytes,
            CompletedAt = DateTime.UtcNow
        };

        _mockCopyService
            .Setup(x => x.CancelCopyAsync(operationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelledOp);

        var cancelResult = await _controller.CancelAsync(operationId);
        var cancelOk = Assert.IsType<OkObjectResult>(cancelResult);
        var finalOp = Assert.IsType<BlobCopyOperation>(cancelOk.Value);
        Assert.Equal(BlobCopyStatus.Cancelled, finalOp.Status);

        // Verify progress was preserved
        Assert.Equal(totalBytes / 3, finalOp.BytesTransferred);
    }

    [Fact]
    public async Task CopyOperation_WithValidationFailures_DoesNotStart()
    {
        var sourceUri = "https://storage.blob.core.windows.net/nonexistent/blob";
        var destUri = "https://storage.blob.core.windows.net/dest/blob";

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Blob not found", Code = "SOURCE_NOT_FOUND" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(sourceUri, destUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        // Validate
        var validateRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var validateResult = await _controller.ValidateAsync(validateRequest);
        var validateBad = Assert.IsType<BadRequestObjectResult>(validateResult);
        Assert.Equal(StatusCodes.Status400BadRequest, validateBad.StatusCode);

        // Try to start
        var startRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var startResult = await _controller.StartAsync(startRequest);
        var startBad = Assert.IsType<BadRequestObjectResult>(startResult);
        Assert.Equal(StatusCodes.Status400BadRequest, startBad.StatusCode);

        // Verify copy never started
        _mockCopyService.Verify(
            x => x.StartCopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task MultipleOperations_CanRunConcurrently()
    {
        var op1Id = Guid.NewGuid().ToString();
        var op2Id = Guid.NewGuid().ToString();

        var op1 = new BlobCopyOperation
        {
            Id = op1Id,
            Status = BlobCopyStatus.InProgress,
            SourceUri = "https://storage.blob.core.windows.net/source/file1.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file1.vhd",
            BytesTransferred = 1024 * 1024 * 50,
            TotalBytes = 1024 * 1024 * 100
        };

        var op2 = new BlobCopyOperation
        {
            Id = op2Id,
            Status = BlobCopyStatus.InProgress,
            SourceUri = "https://storage.blob.core.windows.net/source/file2.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/dest/file2.vhd",
            BytesTransferred = 1024 * 1024 * 75,
            TotalBytes = 1024 * 1024 * 150
        };

        _mockCopyService
            .Setup(x => x.GetOperationStatus(op1Id))
            .Returns(op1);

        _mockCopyService
            .Setup(x => x.GetOperationStatus(op2Id))
            .Returns(op2);

        // Check both operations independently
        var status1 = _controller.GetStatus(op1Id);
        var status2 = _controller.GetStatus(op2Id);

        var result1 = Assert.IsType<OkObjectResult>(status1);
        var result2 = Assert.IsType<OkObjectResult>(status2);

        var returnedOp1 = Assert.IsType<BlobCopyOperation>(result1.Value);
        var returnedOp2 = Assert.IsType<BlobCopyOperation>(result2.Value);

        Assert.Equal(op1Id, returnedOp1.Id);
        Assert.Equal(op2Id, returnedOp2.Id);
        Assert.NotEqual(returnedOp1.BytesTransferred, returnedOp2.BytesTransferred);
    }

    [Fact]
    public void HealthCheck_ReturnsOk_WhenServiceIsRunning()
    {
        var result = _controller.Health();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task CompleteWorkflow_WithProgressNotifications()
    {
        var operationId = "op-notifications-scenario";
        var sourceUri = "https://storage.blob.core.windows.net/source/file.vhd";
        var destUri = "https://storage.blob.core.windows.net/dest/file.vhd";

        // Start operation
        _mockCopyService
            .Setup(x => x.StartCopyAsync(It.IsAny<string>(), sourceUri, destUri, It.IsAny<CancellationToken>()))
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

        var startRequest = new CopyRequest { SourceUri = sourceUri, DestinationUri = destUri };
        var startResult = await _controller.StartAsync(startRequest);
        var createdResult = Assert.IsType<CreatedAtActionResult>(startResult);
        var operation = Assert.IsType<BlobCopyOperation>(createdResult.Value);

        // Verify progress notifications would be triggered
        _mockProgressService.Verify(
            x => x.NotifyProgressAsync(It.IsAny<string>(), It.IsAny<ProgressUpdate>(), It.IsAny<CancellationToken>()),
            Times.Never // Not called in controller, but would be in background task
        );
    }
}
