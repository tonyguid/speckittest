import { useState, useCallback, useEffect, useRef } from 'react';
import { BlobCopyOperation, BlobCopyStatus } from '../types';
import { apiClient } from './apiClient';
import { signalRClient, SignalRCallbacks } from './signalRClient';

/**
 * Hook for managing a blob copy operation lifecycle.
 * Handles starting, tracking, and cancelling copy operations.
 */
export function useCopyOperation() {
  const [operation, setOperation] = useState<BlobCopyOperation | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const signalRSubscribedRef = useRef<string | null>(null);

  /**
   * Starts a new copy operation.
   *
   * @param sourceUri - Source blob URI
   * @param destinationUri - Destination blob URI
   * @param useWebSocket - Whether to use WebSocket for real-time updates (default: true)
   */
  const startOperation = useCallback(
    async (sourceUri: string, destinationUri: string, useWebSocket = true) => {
      setIsLoading(true);
      setError(null);

      try {
        // Start the copy operation
        const op = await apiClient.startCopy({
          sourceUri,
          destinationUri,
        });

        setOperation(op);

        // Setup real-time updates via SignalR if requested
        if (useWebSocket) {
          try {
            if (!signalRClient.isConnected()) {
              await signalRClient.connect(op.id, {
                onProgressUpdate: (progress) => {
                  setOperation((current) =>
                    current
                      ? {
                          ...current,
                          bytesCopied: progress.bytesCopied,
                          totalBytes: progress.totalBytes,
                        }
                      : null
                  );
                },
                onCopyCompleted: (completedOp) => {
                  setOperation(completedOp);
                },
                onCopyFailed: (failedOp) => {
                  setOperation(failedOp);
                  setError(
                    failedOp.errors?.[0]?.message || 'Copy operation failed'
                  );
                },
                onOperationCancelled: (cancelledOp) => {
                  setOperation(cancelledOp);
                },
                onConnectionError: (err) => {
                  console.warn('SignalR error, falling back to polling:', err);
                  // Fall back to polling
                  startPolling(op.id);
                },
              });
            } else {
              await signalRClient.subscribeToOperation(op.id);
            }
            signalRSubscribedRef.current = op.id;
          } catch (err) {
            console.warn('Failed to connect WebSocket, using polling:', err);
            // Fall back to polling
            startPolling(op.id);
          }
        } else {
          startPolling(op.id);
        }
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : String(err);
        setError(errorMessage);
      } finally {
        setIsLoading(false);
      }
    },
    []
  );

  /**
   * Cancels the current operation.
   */
  const cancelOperation = useCallback(async () => {
    if (!operation?.id) {
      setError('No operation to cancel');
      return;
    }

    setIsLoading(true);
    try {
      const cancelled = await apiClient.cancelCopy(operation.id);
      setOperation(cancelled);

      // Cleanup subscription
      if (signalRSubscribedRef.current === operation.id) {
        await signalRClient.unsubscribeFromOperation(operation.id);
        signalRSubscribedRef.current = null;
      }

      // Stop polling
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      setError(errorMessage);
    } finally {
      setIsLoading(false);
    }
  }, [operation?.id]);

  /**
   * Resets the operation state.
   */
  const reset = useCallback(async () => {
    if (operation?.id && signalRSubscribedRef.current === operation.id) {
      try {
        await signalRClient.unsubscribeFromOperation(operation.id);
      } catch (err) {
        console.warn('Error unsubscribing from operation:', err);
      }
      signalRSubscribedRef.current = null;
    }

    if (pollingIntervalRef.current) {
      clearInterval(pollingIntervalRef.current);
      pollingIntervalRef.current = null;
    }

    setOperation(null);
    setError(null);
    setIsLoading(false);
  }, [operation?.id]);

  /**
   * Starts polling for operation status (fallback when WebSocket unavailable).
   */
  const startPolling = useCallback((operationId: string) => {
    // Stop existing polling
    if (pollingIntervalRef.current) {
      clearInterval(pollingIntervalRef.current);
    }

    // Poll every 1 second
    pollingIntervalRef.current = setInterval(async () => {
      try {
        const status = await apiClient.getStatus(operationId);
        setOperation(status);

        // Stop polling if operation is complete
        if (
          status.status === BlobCopyStatus.Completed ||
          status.status === BlobCopyStatus.Failed ||
          status.status === BlobCopyStatus.Cancelled
        ) {
          if (pollingIntervalRef.current) {
            clearInterval(pollingIntervalRef.current);
            pollingIntervalRef.current = null;
          }
        }
      } catch (err) {
        console.error('Error polling operation status:', err);
      }
    }, 1000);
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
  }, []);

  return {
    operation,
    isLoading,
    error,
    startOperation,
    cancelOperation,
    reset,
  };
}
