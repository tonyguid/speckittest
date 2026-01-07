import { BlobCopyOperation, BlobCopyStatus } from '../types.js';
import { useProgress } from '../hooks/useProgress';
import './ProgressDisplay.css';

interface ProgressDisplayProps {
  operation: BlobCopyOperation | null;
  startTime?: number;
  onCancel?: () => void;
  isCancelling?: boolean;
}

/**
 * Component for displaying the progress of a blob copy operation.
 * Shows progress bar, percentage, transfer rate, and estimated time remaining.
 */
export function ProgressDisplay({
  operation,
  startTime,
  onCancel,
  isCancelling = false,
}: ProgressDisplayProps) {
  const progress = useProgress(operation, startTime);

  // Only show when operation is running or pending
  if (!operation || !['Pending', 'Running'].includes(operation.status)) {
    return null;
  }

  const isPending = operation.status === BlobCopyStatus.Pending;
  const isRunning = operation.status === BlobCopyStatus.Running;

  return (
    <div className="progress-display" data-testid="progress-display">
      <div className="progress-header">
        <h3>Copy Progress</h3>
        <p className="operation-id">
          Operation ID: <code>{operation.id}</code>
        </p>
      </div>

      {/* Status Indicator */}
      <div className="status-indicator">
        <span className={`status-badge status-${operation.status.toLowerCase()}`}>
          {isPending ? '⏳ Pending' : '⚙️ Running'}
        </span>
      </div>

      {/* Progress Bar */}
      <div className="progress-container">
        <div
          className={`progress-bar ${progress.isIndeterminate ? 'indeterminate' : ''}`}
          data-testid="progress-bar"
        >
          <div
            className="progress-fill"
            style={{
              width: progress.isIndeterminate ? '0%' : `${progress.percentageComplete}%`,
            }}
          />
        </div>
        <p className="progress-label" data-testid="progress-percentage">
          {progress.isIndeterminate ? 'Calculating...' : `${progress.percentageComplete}%`}
        </p>
      </div>

      {/* Metrics */}
      <div className="metrics-grid">
        {/* Bytes Transferred */}
        <div className="metric">
          <label>Bytes Copied:</label>
          <p data-testid="bytes-transferred">
            {formatBytes(progress.bytesTransferred)} / {formatBytes(progress.totalBytes)}
          </p>
        </div>

        {/* Transfer Rate */}
        {isRunning && (
          <div className="metric">
            <label>Transfer Rate:</label>
            <p data-testid="transfer-rate">
              {progress.transferRateMBps.toFixed(2)} MB/s
            </p>
          </div>
        )}

        {/* Time Remaining */}
        {isRunning && !progress.isIndeterminate && (
          <div className="metric">
            <label>Time Remaining:</label>
            <p data-testid="time-remaining">{progress.estimatedTimeRemainingLabel}</p>
          </div>
        )}

        {/* Total Size */}
        {progress.totalBytes > 0 && (
          <div className="metric">
            <label>Total Size:</label>
            <p>{formatBytes(progress.totalBytes)}</p>
          </div>
        )}
      </div>

      {/* Cancel Button */}
      {isRunning && onCancel && (
        <button
          onClick={onCancel}
          className="btn btn-secondary"
          disabled={isCancelling}
          data-testid="cancel-button"
        >
          {isCancelling ? 'Cancelling...' : 'Cancel Copy'}
        </button>
      )}
    </div>
  );
}

/**
 * Formats bytes to human-readable format.
 */
function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}
