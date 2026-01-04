using Microsoft.AspNetCore.SignalR;
using BlobCopy.API.Models;

namespace BlobCopy.API.Hubs;

/// <summary>
/// SignalR hub for real-time blob copy progress updates.
/// Manages WebSocket connections and broadcasts progress to connected clients.
/// </summary>
public class BlobCopyHub : Hub<IBlobCopyClient>
{
    private readonly ILogger<BlobCopyHub> _logger;
    private static readonly Dictionary<string, HashSet<string>> _operationConnections = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the BlobCopyHub class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics</param>
    public BlobCopyHub(ILogger<BlobCopyHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Sends a connection established message to the client.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected: {Context.ConnectionId}");
        
        try
        {
            await Clients.Caller.ConnectionEstablished(
                "Connected to Blob Copy Hub",
                DateTime.UtcNow,
                Context.ConnectionId
            );
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in OnConnectedAsync for connection {Context.ConnectionId}");
            throw;
        }
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Cleans up any tracked operations for this connection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        
        try
        {
            // Remove this connection from all operation tracking
            lock (_lock)
            {
                var operationsToClean = _operationConnections
                    .Where(kvp => kvp.Value.Contains(Context.ConnectionId))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var operationId in operationsToClean)
                {
                    _operationConnections[operationId].Remove(Context.ConnectionId);
                    if (_operationConnections[operationId].Count == 0)
                    {
                        _operationConnections.Remove(operationId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in OnDisconnectedAsync for connection {Context.ConnectionId}");
        }
    }

    /// <summary>
    /// Allows a client to subscribe to progress updates for a specific copy operation.
    /// Called by client after copy operation is started on the backend.
    /// </summary>
    /// <param name="copyOperationId">ID of the copy operation to track</param>
    public async Task JoinCopyOperation(string copyOperationId)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId))
        {
            _logger.LogWarning($"Client {Context.ConnectionId} attempted to join with empty operation ID");
            return;
        }

        lock (_lock)
        {
            if (!_operationConnections.ContainsKey(copyOperationId))
            {
                _operationConnections[copyOperationId] = new HashSet<string>();
            }
            _operationConnections[copyOperationId].Add(Context.ConnectionId);
        }

        _logger.LogInformation($"Client {Context.ConnectionId} subscribed to operation {copyOperationId}");
        
        // Add connection to a SignalR group for easy broadcasting later
        await Groups.AddToGroupAsync(Context.ConnectionId, $"operation-{copyOperationId}");
    }

    /// <summary>
    /// Allows a client to unsubscribe from progress updates for a specific copy operation.
    /// </summary>
    /// <param name="copyOperationId">ID of the copy operation to stop tracking</param>
    public async Task LeaveCopyOperation(string copyOperationId)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId))
        {
            return;
        }

        lock (_lock)
        {
            if (_operationConnections.ContainsKey(copyOperationId))
            {
                _operationConnections[copyOperationId].Remove(Context.ConnectionId);
                if (_operationConnections[copyOperationId].Count == 0)
                {
                    _operationConnections.Remove(copyOperationId);
                }
            }
        }

        _logger.LogInformation($"Client {Context.ConnectionId} unsubscribed from operation {copyOperationId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"operation-{copyOperationId}");
    }

    /// <summary>
    /// Called by the backend service to broadcast a progress update to all clients tracking this operation.
    /// </summary>
    /// <param name="copyOperationId">ID of the copy operation</param>
    /// <param name="update">Progress update to broadcast</param>
    public async Task BroadcastProgressUpdate(string copyOperationId, ProgressUpdate update)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId) || update == null)
        {
            _logger.LogWarning("Attempted to broadcast progress update with missing data");
            return;
        }

        try
        {
            // Send to all clients in this operation's group
            await Clients.Group($"operation-{copyOperationId}").ProgressUpdate(update);
            _logger.LogDebug($"Broadcasted progress update for operation {copyOperationId}: {update.ProgressPercentage}%");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting progress update for operation {copyOperationId}");
        }
    }

    /// <summary>
    /// Called by the backend service to notify all clients that a copy operation completed successfully.
    /// </summary>
    public async Task BroadcastCopyCompleted(string copyOperationId, string destinationUri, long totalBytes, int durationSeconds, DateTime completedAt)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId))
        {
            _logger.LogWarning("Attempted to broadcast copy completed with missing operation ID");
            return;
        }

        try
        {
            await Clients.Group($"operation-{copyOperationId}").CopyCompleted(
                copyOperationId,
                destinationUri,
                totalBytes,
                durationSeconds,
                completedAt
            );
            _logger.LogInformation($"Broadcasted copy completion for operation {copyOperationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting copy completion for operation {copyOperationId}");
        }
    }

    /// <summary>
    /// Called by the backend service to notify all clients that a copy operation failed.
    /// </summary>
    public async Task BroadcastCopyFailed(string copyOperationId, string errorCode, string errorMessage, bool retryable, DateTime failedAt)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId))
        {
            _logger.LogWarning("Attempted to broadcast copy failed with missing operation ID");
            return;
        }

        try
        {
            await Clients.Group($"operation-{copyOperationId}").CopyFailed(
                copyOperationId,
                errorCode,
                errorMessage,
                retryable,
                failedAt
            );
            _logger.LogInformation($"Broadcasted copy failure for operation {copyOperationId}: {errorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting copy failure for operation {copyOperationId}");
        }
    }

    /// <summary>
    /// Called by the backend service to notify all clients that a copy operation was cancelled.
    /// </summary>
    public async Task BroadcastOperationCancelled(string copyOperationId, long bytesTransferred, DateTime cancelledAt, string message)
    {
        if (string.IsNullOrWhiteSpace(copyOperationId))
        {
            _logger.LogWarning("Attempted to broadcast operation cancelled with missing operation ID");
            return;
        }

        try
        {
            await Clients.Group($"operation-{copyOperationId}").OperationCancelled(
                copyOperationId,
                bytesTransferred,
                cancelledAt,
                message
            );
            _logger.LogInformation($"Broadcasted cancellation for operation {copyOperationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error broadcasting cancellation for operation {copyOperationId}");
        }
    }

    /// <summary>
    /// Returns the number of connected clients for a specific operation (for testing/monitoring).
    /// </summary>
    public int GetConnectedClientsForOperation(string copyOperationId)
    {
        lock (_lock)
        {
            return _operationConnections.ContainsKey(copyOperationId)
                ? _operationConnections[copyOperationId].Count
                : 0;
        }
    }

    /// <summary>
    /// Returns the total number of connected clients (for testing/monitoring).
    /// </summary>
    public int GetTotalConnectedClients()
    {
        lock (_lock)
        {
            return _operationConnections.Values.Sum(s => s.Count);
        }
    }
}
