using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobCopy.API.Models;
using System.Diagnostics;

namespace BlobCopy.API.Services;

/// <summary>
/// Service for managing blob copy operations with streaming and progress tracking.
/// Handles chunked downloads and uploads with real-time progress updates.
/// </summary>
public interface IBlobCopyService
{
    /// <summary>
    /// Starts a copy operation from source to destination blob.
    /// </summary>
    /// <param name="operationId">Unique operation ID</param>
    /// <param name="sourceUri">Source blob URI</param>
    /// <param name="destinationUri">Destination blob URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>BlobCopyOperation with metadata</returns>
    Task<BlobCopyOperation> StartCopyAsync(
        string operationId,
        string sourceUri,
        string destinationUri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Performs streaming copy of blob data with progress tracking.
    /// </summary>
    /// <param name="operation">Copy operation context</param>
    /// <param name="onProgressUpdate">Callback for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated BlobCopyOperation with completion status</returns>
    Task<BlobCopyOperation> ExecuteCopyAsync(
        BlobCopyOperation operation,
        Func<long, long, Task>? onProgressUpdate = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cancels an in-progress copy operation.
    /// </summary>
    /// <param name="operationId">Operation ID to cancel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancelled BlobCopyOperation</returns>
    Task<BlobCopyOperation> CancelCopyAsync(
        string operationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the current progress of a copy operation.
    /// </summary>
    /// <param name="operationId">Operation ID</param>
    /// <returns>Current BlobCopyOperation status</returns>
    BlobCopyOperation? GetOperationStatus(string operationId);
}

/// <summary>
/// Implementation of IBlobCopyService.
/// </summary>
public class BlobCopyService : IBlobCopyService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobCopyService> _logger;
    
    // Dictionary to track in-memory operations
    private readonly Dictionary<string, BlobCopyOperation> _operations = new();
    private readonly object _operationsLock = new();

    // Constants for chunking
    private const int ChunkSize = 4 * 1024 * 1024; // 4 MB chunks
    private const int MaxParallelChunks = 4;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    /// <summary>
    /// Initializes a new instance of BlobCopyService.
    /// </summary>
    public BlobCopyService(
        BlobServiceClient blobServiceClient,
        ILogger<BlobCopyService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BlobCopyOperation> StartCopyAsync(
        string operationId,
        string sourceUri,
        string destinationUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));
        if (string.IsNullOrWhiteSpace(sourceUri))
            throw new ArgumentException("Source URI is required", nameof(sourceUri));
        if (string.IsNullOrWhiteSpace(destinationUri))
            throw new ArgumentException("Destination URI is required", nameof(destinationUri));

        var operation = new BlobCopyOperation
        {
            Id = operationId,
            SourceUri = sourceUri,
            DestinationUri = destinationUri,
            Status = BlobCopyStatus.Pending,
            StartedAt = DateTime.UtcNow,
            BytesCopied = 0,
            TotalBytes = 0,
            Errors = new List<ValidationError>()
        };

        // Get source blob size
        try
        {
            var sourceBlob = new BlobClient(new Uri(sourceUri));
            var properties = await sourceBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            operation.TotalBytes = properties.Value.ContentLength;
            operation.SourceBlobName = sourceBlob.Name;

            _logger.LogInformation(
                "Copy operation {OperationId} initialized. Source: {Source}, Destination: {Destination}, Size: {Size} bytes",
                operationId,
                sourceUri,
                destinationUri,
                operation.TotalBytes
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing copy operation {OperationId}", operationId);
            operation.Status = BlobCopyStatus.Failed;
            operation.Errors.Add(new ValidationError
            {
                Field = "sourceUri",
                Message = ex.Message,
                Code = "INITIALIZATION_ERROR"
            });
        }

        lock (_operationsLock)
        {
            _operations[operationId] = operation;
        }

        return operation;
    }

    public async Task<BlobCopyOperation> ExecuteCopyAsync(
        BlobCopyOperation operation,
        Func<long, long, Task>? onProgressUpdate = null,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            operation.Status = BlobCopyStatus.Running;
            var stopwatch = Stopwatch.StartNew();

            var sourceBlob = new BlobClient(new Uri(operation.SourceUri));
            var destUri = new Uri(operation.DestinationUri);
            var destContainerClient = _blobServiceClient.GetBlobContainerClient(
                destUri.PathAndQuery.Split('/')[1]
            );

            var destBlobName = operation.SourceBlobName ?? Path.GetFileName(sourceBlob.Name);
            var destBlobClient = destContainerClient.GetBlobClient(destBlobName);

            // Get source blob properties
            var sourceProperties = await sourceBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            operation.TotalBytes = sourceProperties.Value.ContentLength;

            _logger.LogInformation(
                "Starting copy operation {OperationId}. Total bytes: {TotalBytes}",
                operation.Id,
                operation.TotalBytes
            );

            // Download and upload with streaming
            using (var sourceStream = await sourceBlob.OpenReadAsync(
                new BlobOpenReadOptions(allowModifications: false),
                cancellationToken))
            {
                // Upload blob with streaming
                await destBlobClient.UploadAsync(
                    sourceStream,
                    overwrite: true,
                    cancellationToken: cancellationToken
                );

                operation.BytesCopied = operation.TotalBytes;
            }

            stopwatch.Stop();

            operation.Status = BlobCopyStatus.Completed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;

            var transferRateMbps = operation.TotalBytes > 0
                ? (operation.TotalBytes / (1024.0 * 1024.0)) / stopwatch.Elapsed.TotalSeconds
                : 0;

            _logger.LogInformation(
                "Copy operation {OperationId} completed. Duration: {DurationSeconds}s, Rate: {TransferRateMbps} MB/s",
                operation.Id,
                operation.DurationSeconds,
                transferRateMbps.ToString("F2")
            );

            // Final progress update
            if (onProgressUpdate != null)
            {
                await onProgressUpdate(operation.BytesCopied, operation.TotalBytes);
            }

            lock (_operationsLock)
            {
                _operations[operation.Id] = operation;
            }

            return operation;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Copy operation {OperationId} was cancelled", operation.Id);
            operation.Status = BlobCopyStatus.Cancelled;
            operation.CompletedAt = DateTime.UtcNow;
            operation.Errors.Add(new ValidationError
            {
                Field = "operation",
                Message = "Copy operation was cancelled by user",
                Code = "OPERATION_CANCELLED"
            });

            lock (_operationsLock)
            {
                _operations[operation.Id] = operation;
            }

            return operation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing copy operation {OperationId}", operation.Id);
            operation.Status = BlobCopyStatus.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.Errors.Add(new ValidationError
            {
                Field = "operation",
                Message = ex.Message,
                Code = "COPY_ERROR"
            });

            lock (_operationsLock)
            {
                _operations[operation.Id] = operation;
            }

            return operation;
        }
    }

    public async Task<BlobCopyOperation> CancelCopyAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));

        lock (_operationsLock)
        {
            if (!_operations.TryGetValue(operationId, out var operation))
            {
                var notFoundOp = new BlobCopyOperation
                {
                    Id = operationId,
                    Status = BlobCopyStatus.NotFound
                };
                _logger.LogWarning("Cancel requested for non-existent operation {OperationId}", operationId);
                return notFoundOp;
            }

            if (operation.Status == BlobCopyStatus.Running || operation.Status == BlobCopyStatus.Pending)
            {
                operation.Status = BlobCopyStatus.Cancelled;
                operation.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Copy operation {OperationId} cancelled", operationId);
            }
            else if (operation.Status == BlobCopyStatus.Completed)
            {
                _logger.LogWarning(
                    "Cannot cancel completed operation {OperationId}",
                    operationId
                );
                operation.Errors.Add(new ValidationError
                {
                    Field = "operation",
                    Message = "Cannot cancel a completed operation",
                    Code = "INVALID_STATE_TRANSITION"
                });
            }

            return operation;
        }
    }

    public BlobCopyOperation? GetOperationStatus(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));

        lock (_operationsLock)
        {
            _operations.TryGetValue(operationId, out var operation);
            return operation;
        }
    }
}
