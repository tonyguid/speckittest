import { useMemo } from 'react';
import { BlobCopyOperation } from '../types';

/**
 * Progress metrics calculated from a blob copy operation.
 */
export interface ProgressMetrics {
  percentageComplete: number;
  bytesTransferred: number;
  totalBytes: number;
  byteRemaining: number;
  transferRateMBps: number;
  estimatedTimeRemainingSec: number;
  estimatedTimeRemainingLabel: string;
  isIndeterminate: boolean;
}

/**
 * Hook for calculating progress metrics from a blob copy operation.
 * Provides percentage complete, transfer rate, and ETA.
 *
 * @param operation - The current blob copy operation
 * @param startTime - Optional start time (Unix timestamp in ms) for rate calculation
 */
export function useProgress(
  operation: BlobCopyOperation | null,
  startTime?: number
): ProgressMetrics {
  return useMemo(() => {
    if (!operation) {
      return {
        percentageComplete: 0,
        bytesTransferred: 0,
        totalBytes: 0,
        byteRemaining: 0,
        transferRateMBps: 0,
        estimatedTimeRemainingSec: 0,
        estimatedTimeRemainingLabel: '--:--',
        isIndeterminate: true,
      };
    }

    const bytesCopied = operation.bytesCopied || 0;
    const totalBytes = operation.totalBytes || 0;

    // Calculate percentage (handle edge case where totalBytes is 0)
    const percentageComplete =
      totalBytes > 0 ? Math.round((bytesCopied / totalBytes) * 100) : 0;

    const byteRemaining = Math.max(0, totalBytes - bytesCopied);

    // Calculate transfer rate (bytes per second)
    let transferRateMBps = 0;
    let elapsedSeconds = 0;

    if (startTime) {
      elapsedSeconds = (Date.now() - startTime) / 1000;
      if (elapsedSeconds > 0) {
        const bytesPerSecond = bytesCopied / elapsedSeconds;
        transferRateMBps = bytesPerSecond / (1024 * 1024); // Convert to MB/s
      }
    }

    // Calculate estimated time remaining
    let estimatedTimeRemainingSec = 0;
    let estimatedTimeRemainingLabel = '--:--';

    if (transferRateMBps > 0 && byteRemaining > 0) {
      const remainingBytes = byteRemaining;
      const remainingSeconds = remainingBytes / (transferRateMBps * 1024 * 1024);
      estimatedTimeRemainingSec = Math.ceil(remainingSeconds);

      // Format as MM:SS
      const minutes = Math.floor(estimatedTimeRemainingSec / 60);
      const seconds = estimatedTimeRemainingSec % 60;
      estimatedTimeRemainingLabel = `${minutes}:${seconds.toString().padStart(2, '0')}`;
    }

    // Indeterminate progress: operation running but no size info yet
    const isIndeterminate = operation.status === 'Running' && totalBytes === 0;

    return {
      percentageComplete,
      bytesTransferred: bytesCopied,
      totalBytes,
      byteRemaining,
      transferRateMBps: Math.round(transferRateMBps * 100) / 100, // Round to 2 decimals
      estimatedTimeRemainingSec,
      estimatedTimeRemainingLabel,
      isIndeterminate,
    };
  }, [operation, startTime]);
}
