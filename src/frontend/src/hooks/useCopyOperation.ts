import { useState, useCallback, useEffect, useRef } from 'react';
import { BlobCopyOperation, BlobCopyStatus } from '../types';
import { apiClient } from './apiClient';
import { signalRClient, SignalRCallbacks } from './signalRClient';
import { appInsightsService } from '../services/applicationInsightsService';

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

        // Track telemetry event
        appInsightsService.trackCopyOperationStarted(
          op.id,
          sourceUri,
          destinationUri,
          op.totalBytes
        );

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
                  appInsightsService.trackProgressUpdate(progress);
                },
                onCopyCompleted: (completedOp) => {
                  setOperation(completedOp);
                  const durationSeconds = op.startedAt
                    ? (new Date(completedOp.completedAt || '').getTime() - new Date(op.startedAt).getTime()) / 1000
                    : 0;
                  appInsightsService.trackCopyOperationCompleted(completedOp, durationSeconds);
                },
                onCopyFailed: (failedOp) => {
                  setOperation(failedOp);
                  const errorMsg = failedOp.errors?.[0]?.message || 'Copy operation failed';
                  setError(errorMsg);
                  const durationSeconds = op.startedAt
                    ? (new Date(failedOp.completedAt || '').getTime() - new Date(op.startedAt).getTime()) / 1000
                    : 0;
                  appInsightsService.trackCopyOperationFailed(failedOp, errorMsg, durationSeconds);
                },
                onOperationCancelled: (cancelledOp) => {
                  setOperation(cancelledOp);
                  const durationSeconds = op.startedAt
                    ? (new Date(cancelledOp.completedAt || '').getTime() - new Date(op.startedAt).getTime()) / 1000
                    : 0;
                  appInsightsService.trackCopyOperationCancelled(cancelledOp, durationSeconds);
                },
                onConnectionError: (err) => {
                  console.warn('SignalR error, falling back to polling:', err);
                  appInsightsService.trackException(err);
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
            appInsightsService.trackException(err instanceof Error ? err : new Error(String(err)));
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
