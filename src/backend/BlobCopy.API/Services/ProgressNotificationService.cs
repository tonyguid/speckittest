using BlobCopy.API.Hubs;
using BlobCopy.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace BlobCopy.API.Services;

/// <summary>
/// Service for managing real-time progress notifications via SignalR.
/// Broadcasts progress updates to subscribed clients.
/// </summary>
public interface IProgressNotificationService
{
    /// <summary>
    /// Notifies all subscribed clients of progress update.
    /// </summary>
    /// <param name="operationId">Operation ID</param>
    /// <param name="progress">Progress update with bytes copied and total bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Awaitable task</returns>
    Task NotifyProgressAsync(
        string operationId,
        ProgressUpdate progress,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Notifies all subscribed clients of successful completion.
    /// </summary>
    /// <param name="operationId">Operation ID</param>
    /// <param name="operation">Completed operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Awaitable task</returns>
    Task NotifyCompletionAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Notifies all subscribed clients of copy failure.
    /// </summary>
    /// <param name="operationId">Operation ID</param>
    /// <param name="operation">Failed operation with error details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Awaitable task</returns>
    Task NotifyFailureAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Notifies all subscribed clients of operation cancellation.
    /// </summary>
    /// <param name="operationId">Operation ID</param>
    /// <param name="operation">Cancelled operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Awaitable task</returns>
    Task NotifyCancellationAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of IProgressNotificationService using SignalR.
/// </summary>
public class ProgressNotificationService : IProgressNotificationService
{
    private readonly IHubContext<BlobCopyHub, IBlobCopyClient> _hubContext;
    private readonly ILogger<ProgressNotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of ProgressNotificationService.
    /// </summary>
    public ProgressNotificationService(
        IHubContext<BlobCopyHub, IBlobCopyClient> hubContext,
        ILogger<ProgressNotificationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyProgressAsync(
        string operationId,
        ProgressUpdate progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));
        if (progress == null)
            throw new ArgumentNullException(nameof(progress));

        try
        {
            _logger.LogDebug(
                "Sending progress update for operation {OperationId}. Progress: {BytesTransferred}/{TotalBytes} bytes ({Percentage:P})",
                operationId,
                progress.BytesTransferred,
                progress.TotalBytes,
                progress.TotalBytes > 0 ? (double)progress.BytesTransferred / progress.TotalBytes : 0
            );

            // Send to all clients subscribed to this operation
            await _hubContext.Clients
                .Group(operationId)
                .ProgressUpdate(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending progress update for operation {OperationId}", operationId);
            throw;
        }
    }

    public async Task NotifyCompletionAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            var durationSeconds = operation.CompletedAt != null
                ? (operation.CompletedAt.Value - operation.StartedAt).TotalSeconds
                : 0;

            var transferRateMbps = durationSeconds > 0 && operation.TotalBytes > 0
                ? (operation.TotalBytes / (1024.0 * 1024.0)) / durationSeconds
                : 0;

            _logger.LogInformation(
                "Copy operation {OperationId} completed successfully. " +
                "Size: {TotalBytes} bytes, Duration: {DurationSeconds:F2}s, Rate: {TransferRateMbps:F2} MB/s",
                operationId,
                operation.TotalBytes,
                durationSeconds,
                transferRateMbps
            );

            await _hubContext.Clients
                .Group(operationId)
                .CopyCompleted(
                    operationId,
                    operation.DestinationUri,
                    operation.TotalBytes,
                    (int)durationSeconds,
                    operation.CompletedAt ?? DateTime.UtcNow
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying completion for operation {OperationId}", operationId);
            throw;
        }
    }

    public async Task NotifyFailureAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            var errorMessage = operation.ErrorMessage ?? "Unknown error";
            _logger.LogError(
                "Copy operation {OperationId} failed. Error: {ErrorMessage}. " +
                "BytesTransferred: {BytesTransferred}/{TotalBytes}",
                operationId,
                errorMessage,
                operation.BytesTransferred,
                operation.TotalBytes
            );

            await _hubContext.Clients
                .Group(operationId)
                .CopyFailed(
                    operationId,
                    operation.ErrorCode ?? "UNKNOWN_ERROR",
                    errorMessage,
                    false,
                    operation.CompletedAt ?? DateTime.UtcNow
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying failure for operation {OperationId}", operationId);
            throw;
        }
    }

    public async Task NotifyCancellationAsync(
        string operationId,
        BlobCopyOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID is required", nameof(operationId));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            _logger.LogInformation(
                "Copy operation {OperationId} was cancelled. " +
                "BytesTransferred: {BytesTransferred}/{TotalBytes}",
                operationId,
                operation.BytesTransferred,
                operation.TotalBytes
            );

            await _hubContext.Clients
                .Group(operationId)
                .OperationCancelled(
                    operationId,
                    operation.BytesTransferred,
                    operation.CompletedAt ?? DateTime.UtcNow,
                    "Operation was cancelled by user"
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying cancellation for operation {OperationId}", operationId);
            throw;
        }
    }
}
