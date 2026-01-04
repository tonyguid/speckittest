using BlobCopy.API.Models;

namespace BlobCopy.API.Hubs;

/// <summary>
/// Client methods that can be called from the server to connected SignalR clients.
/// Implemented by the frontend JavaScript/TypeScript client.
/// </summary>
public interface IBlobCopyClient
{
    /// <summary>
    /// Called by server to notify client of progress updates during an active copy operation.
    /// Sent every 1-5 seconds during active copy.
    /// </summary>
    /// <param name="update">Progress update containing bytes transferred, percentage, transfer rate, etc.</param>
    Task ProgressUpdate(ProgressUpdate update);

    /// <summary>
    /// Called by server to notify client that a copy operation completed successfully.
    /// Client should stop polling/waiting and display success message.
    /// </summary>
    /// <param name="copyOperationId">ID of the completed operation</param>
    /// <param name="destinationUri">URI of the copied blob</param>
    /// <param name="totalBytes">Total bytes copied</param>
    /// <param name="durationSeconds">Duration of the copy operation in seconds</param>
    /// <param name="completedAt">Timestamp of completion (ISO 8601)</param>
    Task CopyCompleted(string copyOperationId, string destinationUri, long totalBytes, int durationSeconds, DateTime completedAt);

    /// <summary>
    /// Called by server to notify client that a copy operation encountered an error.
    /// Client should stop polling/waiting and display error message.
    /// </summary>
    /// <param name="copyOperationId">ID of the failed operation</param>
    /// <param name="errorCode">Machine-readable error code (e.g., "SOURCE_NOT_FOUND")</param>
    /// <param name="errorMessage">Human-readable error message</param>
    /// <param name="retryable">Whether the error is retryable (transient vs permanent)</param>
    /// <param name="failedAt">Timestamp of failure (ISO 8601)</param>
    Task CopyFailed(string copyOperationId, string errorCode, string errorMessage, bool retryable, DateTime failedAt);

    /// <summary>
    /// Called by server to notify client that a copy operation was cancelled by the user.
    /// </summary>
    /// <param name="copyOperationId">ID of the cancelled operation</param>
    /// <param name="bytesTransferred">Bytes transferred before cancellation</param>
    /// <param name="cancelledAt">Timestamp of cancellation (ISO 8601)</param>
    /// <param name="message">Message to display to user</param>
    Task OperationCancelled(string copyOperationId, long bytesTransferred, DateTime cancelledAt, string message);

    /// <summary>
    /// Called by server when connection is established successfully.
    /// Client can use this to mark the connection as ready.
    /// </summary>
    /// <param name="message">Connection established message</param>
    /// <param name="serverTime">Server timestamp (ISO 8601)</param>
    /// <param name="connectionId">Unique connection ID assigned by SignalR</param>
    Task ConnectionEstablished(string message, DateTime serverTime, string connectionId);
}
