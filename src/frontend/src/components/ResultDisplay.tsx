import { BlobCopyOperation, BlobCopyStatus, ValidationError } from '../types';
import './ResultDisplay.css';

interface ResultDisplayProps {
  operation: BlobCopyOperation | null;
  durationSeconds?: number;
}

/**
 * Component for displaying the result of a blob copy operation.
 * Shows success message, error details, or cancellation status.
 */
export function ResultDisplay({
  operation,
  durationSeconds,
}: ResultDisplayProps) {
  if (!operation) {
    return null;
  }

  const isSuccess = operation.status === BlobCopyStatus.Completed;
  const isFailed = operation.status === BlobCopyStatus.Failed;
  const isCancelled = operation.status === BlobCopyStatus.Cancelled;

  // Only show when operation is complete
  if (!isSuccess && !isFailed && !isCancelled) {
    return null;
  }

  return (
    <div
      className={`result-display result-${operation.status.toLowerCase()}`}
      data-testid="result-display"
      role="status"
    >
      {/* Success Result */}
      {isSuccess && (
        <div className="result-success">
          <h2>✅ Copy Completed Successfully</h2>
          <div className="result-details">
            <p>
              <strong>Source:</strong> <code>{operation.sourceUri}</code>
            </p>
            <p>
              <strong>Destination:</strong> <code>{operation.destinationUri}</code>
            </p>
            {operation.bytesCopied && (
              <p>
                <strong>Bytes Copied:</strong> {formatBytes(operation.bytesCopied)}
              </p>
            )}
            {operation.totalBytes && (
              <p>
                <strong>Total Size:</strong> {formatBytes(operation.totalBytes)}
              </p>
            )}
            {durationSeconds && (
              <p>
                <strong>Duration:</strong> {formatDuration(durationSeconds)}
              </p>
            )}
            {operation.completedAt && (
              <p>
                <strong>Completed At:</strong>{' '}
                {new Date(operation.completedAt).toLocaleString()}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Failed Result */}
      {isFailed && (
        <div className="result-failure">
          <h2>❌ Copy Failed</h2>
          <div className="result-details">
            <p>
              <strong>Source:</strong> <code>{operation.sourceUri}</code>
            </p>
            <p>
              <strong>Destination:</strong> <code>{operation.destinationUri}</code>
            </p>
            {operation.errors && operation.errors.length > 0 && (
              <div className="error-list" data-testid="error-list">
                <strong>Errors:</strong>
                <ul>
                  {operation.errors.map((err: ValidationError, idx: number) => (
                    <li key={idx}>
                      <p className="error-message">{err.message}</p>
                      {err.field && (
                        <p className="error-field">
                          <em>Field: {err.field}</em>
                        </p>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {durationSeconds && (
              <p>
                <strong>Duration Before Failure:</strong>{' '}
                {formatDuration(durationSeconds)}
              </p>
            )}
            {operation.completedAt && (
              <p>
                <strong>Failed At:</strong>{' '}
                {new Date(operation.completedAt).toLocaleString()}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Cancelled Result */}
      {isCancelled && (
        <div className="result-cancelled">
          <h2>⚠️ Copy Cancelled</h2>
          <div className="result-details">
            <p>
              <strong>Source:</strong> <code>{operation.sourceUri}</code>
            </p>
            <p>
              <strong>Destination:</strong> <code>{operation.destinationUri}</code>
            </p>
            {operation.bytesCopied && (
              <p>
                <strong>Bytes Copied Before Cancellation:</strong>{' '}
                {formatBytes(operation.bytesCopied)} /{' '}
                {operation.totalBytes && formatBytes(operation.totalBytes)}
              </p>
            )}
            {durationSeconds && (
              <p>
                <strong>Duration:</strong> {formatDuration(durationSeconds)}
              </p>
            )}
            {operation.completedAt && (
              <p>
                <strong>Cancelled At:</strong>{' '}
                {new Date(operation.completedAt).toLocaleString()}
              </p>
            )}
          </div>
        </div>
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

/**
 * Formats seconds to human-readable duration (HH:MM:SS).
 */
function formatDuration(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = Math.floor(seconds % 60);

  const parts = [];
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  parts.push(`${secs}s`);

  return parts.join(' ');
}
