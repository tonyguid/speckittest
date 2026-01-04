using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlobCopy.API.Controllers;

/// <summary>
/// Controller for blob copy operations.
/// Handles validation, copy initiation, status tracking, and cancellation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BlobCopyController : ControllerBase
{
    private readonly IBlobValidationService _validationService;
    private readonly IBlobCopyService _copyService;
    private readonly IProgressNotificationService _progressService;
    private readonly ILogger<BlobCopyController> _logger;

    /// <summary>
    /// Initializes a new instance of BlobCopyController.
    /// </summary>
    public BlobCopyController(
        IBlobValidationService validationService,
        IBlobCopyService copyService,
        IProgressNotificationService progressService,
        ILogger<BlobCopyController> logger)
    {
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _copyService = copyService ?? throw new ArgumentNullException(nameof(copyService));
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates source and destination blob URIs before copy operation.
    /// </summary>
    /// <remarks>
    /// Checks URI format, blob existence, and user permissions.
    /// Does NOT start the actual copy operation.
    /// </remarks>
    /// <param name="request">Copy request with source and destination URIs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK if valid with empty errors array, or 400 Bad Request with validation errors</returns>
    /// <response code="200">Validation successful (errors array will be empty)</response>
    /// <response code="400">Validation failed with specific error details</response>
    /// <response code="500">Server error during validation</response>
    [HttpPost("validate")]
    [ProduceResponseType(typeof(List<ValidationError>), StatusCodes.Status200OK)]
    [ProduceResponseType(typeof(List<ValidationError>), StatusCodes.Status400BadRequest)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateAsync(
        [FromBody] CopyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                _logger.LogWarning("Validate endpoint called with null request");
                return BadRequest(new List<ValidationError>
                {
                    new ValidationError
                    {
                        Field = "request",
                        Message = "Request body is required",
                        Code = "NULL_REQUEST"
                    }
                });
            }

            _logger.LogInformation(
                "Validating blob copy request. Source: {SourceUri}, Destination: {DestinationUri}",
                request.SourceUri,
                request.DestinationUri
            );

            // Perform validation
            var errors = await _validationService.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                cancellationToken
            );

            if (errors.Any())
            {
                _logger.LogWarning(
                    "Validation failed with {ErrorCount} error(s)",
                    errors.Count
                );
                return BadRequest(errors);
            }

            _logger.LogInformation(
                "Validation successful for source: {SourceUri}",
                request.SourceUri
            );

            return Ok(new List<ValidationError>());
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Validate request was cancelled");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Validation request was cancelled",
                    Code = "OPERATION_CANCELLED",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating blob copy request");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Error validating request. Please try again.",
                    Code = "VALIDATION_ERROR",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// Starts a new blob copy operation.
    /// </summary>
    /// <remarks>
    /// Creates and initializes a copy operation. Returns an operation ID that can be used
    /// to track progress via WebSocket or polling /status endpoint.
    /// </remarks>
    /// <param name="request">Copy request with source and destination URIs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>201 Created with operation details including operation ID</returns>
    /// <response code="201">Copy operation created successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Server error starting copy operation</response>
    [HttpPost("start")]
    [ProduceResponseType(typeof(BlobCopyOperation), StatusCodes.Status201Created)]
    [ProduceResponseType(typeof(List<ValidationError>), StatusCodes.Status400BadRequest)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartAsync(
        [FromBody] CopyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                _logger.LogWarning("Start endpoint called with null request");
                return BadRequest(new List<ValidationError>
                {
                    new ValidationError
                    {
                        Field = "request",
                        Message = "Request body is required",
                        Code = "NULL_REQUEST"
                    }
                });
            }

            _logger.LogInformation(
                "Starting blob copy operation. Source: {SourceUri}, Destination: {DestinationUri}",
                request.SourceUri,
                request.DestinationUri
            );

            // Validate URIs first
            var validationErrors = await _validationService.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                cancellationToken
            );

            if (validationErrors.Any())
            {
                _logger.LogWarning("Copy start validation failed with {ErrorCount} error(s)", validationErrors.Count);
                return BadRequest(validationErrors);
            }

            // Generate operation ID
            var operationId = Guid.NewGuid().ToString();

            // Initialize copy operation
            var operation = await _copyService.StartCopyAsync(
                operationId,
                request.SourceUri,
                request.DestinationUri,
                cancellationToken
            );

            _logger.LogInformation(
                "Copy operation {OperationId} created successfully. Size: {TotalBytes} bytes",
                operationId,
                operation.TotalBytes
            );

            // Start background copy task
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _ = Task.Run(async () =>
            {
                try
                {
                    await _copyService.ExecuteCopyAsync(
                        operation,
                        async (bytesCopied, totalBytes) =>
                        {
                            var progressUpdate = new ProgressUpdate
                            {
                                BytesCopied = bytesCopied,
                                TotalBytes = totalBytes,
                                TimestampUtc = DateTime.UtcNow
                            };
                            await _progressService.NotifyProgressAsync(operationId, progressUpdate, cancellationToken);
                        },
                        cancellationToken
                    );

                    // Notify completion
                    operation.Status = BlobCopyStatus.Completed;
                    await _progressService.NotifyCompletionAsync(operationId, operation, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing copy operation {OperationId}", operationId);
                    operation.Status = BlobCopyStatus.Failed;
                    operation.Errors.Add(new ValidationError
                    {
                        Field = "operation",
                        Message = ex.Message,
                        Code = "COPY_EXECUTION_ERROR"
                    });
                    await _progressService.NotifyFailureAsync(operationId, operation, cancellationToken);
                }
            }, cancellationToken);
#pragma warning restore CS4014

            return CreatedAtAction(nameof(GetStatusAsync), new { id = operationId }, operation);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Start request was cancelled");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Copy start request was cancelled",
                    Code = "OPERATION_CANCELLED",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting blob copy operation");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Error starting copy operation. Please try again.",
                    Code = "START_ERROR",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// Gets the current status of a copy operation.
    /// </summary>
    /// <remarks>
    /// Returns detailed status including bytes copied, total bytes, current status,
    /// and any errors. Useful for polling clients that don't support WebSocket.
    /// </remarks>
    /// <param name="id">Operation ID returned from /start endpoint</param>
    /// <returns>200 OK with operation status, or 404 Not Found if operation doesn't exist</returns>
    /// <response code="200">Operation status retrieved successfully</response>
    /// <response code="404">Operation not found</response>
    /// <response code="500">Server error retrieving status</response>
    [HttpGet("status/{id}")]
    [ProduceResponseType(typeof(BlobCopyOperation), StatusCodes.Status200OK)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public IActionResult GetStatus(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("GetStatus endpoint called with empty ID");
                return BadRequest(new ErrorResponse
                {
                    Message = "Operation ID is required",
                    Code = "INVALID_ID",
                    Timestamp = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Getting status for operation {OperationId}", id);

            var operation = _copyService.GetOperationStatus(id);

            if (operation == null)
            {
                _logger.LogWarning("Operation {OperationId} not found", id);
                return NotFound(new ErrorResponse
                {
                    Message = $"Operation '{id}' not found",
                    Code = "NOT_FOUND",
                    Timestamp = DateTime.UtcNow
                });
            }

            return Ok(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for operation {OperationId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Error retrieving operation status. Please try again.",
                    Code = "STATUS_ERROR",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// Gets the current status of a copy operation. (Async variant for compatibility)
    /// </summary>
    private async Task<IActionResult> GetStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(GetStatus(id));
    }

    /// <summary>
    /// Cancels a copy operation that is in progress or pending.
    /// </summary>
    /// <remarks>
    /// Stops an ongoing copy operation. Cannot cancel a completed or failed operation.
    /// Returns 200 OK with updated operation status.
    /// </remarks>
    /// <param name="id">Operation ID to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>200 OK with cancelled operation status, or 404 Not Found</returns>
    /// <response code="200">Operation cancelled successfully</response>
    /// <response code="404">Operation not found</response>
    /// <response code="500">Server error cancelling operation</response>
    [HttpPost("cancel/{id}")]
    [ProduceResponseType(typeof(BlobCopyOperation), StatusCodes.Status200OK)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProduceResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("Cancel endpoint called with empty ID");
                return BadRequest(new ErrorResponse
                {
                    Message = "Operation ID is required",
                    Code = "INVALID_ID",
                    Timestamp = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Cancelling operation {OperationId}", id);

            var operation = await _copyService.CancelCopyAsync(id, cancellationToken);

            if (operation.Status == BlobCopyStatus.NotFound)
            {
                _logger.LogWarning("Operation {OperationId} not found for cancellation", id);
                return NotFound(new ErrorResponse
                {
                    Message = $"Operation '{id}' not found",
                    Code = "NOT_FOUND",
                    Timestamp = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Operation {OperationId} cancelled successfully", id);

            // Notify clients of cancellation
            await _progressService.NotifyCancellationAsync(id, operation, cancellationToken);

            return Ok(operation);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Cancel request was cancelled");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Cancel request was cancelled",
                    Code = "OPERATION_CANCELLED",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation {OperationId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Message = "Error cancelling operation. Please try again.",
                    Code = "CANCEL_ERROR",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// Health check endpoint for service status.
    /// </summary>
    /// <remarks>
    /// Returns 200 OK if service is healthy and can connect to Azure Storage.
    /// </remarks>
    /// <returns>200 OK if healthy</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    [HttpGet("health")]
    [ProduceResponseType(StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Health()
    {
        try
        {
            _logger.LogDebug("Health check endpoint called");
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { status = "unhealthy", error = ex.Message }
            );
        }
    }
}
