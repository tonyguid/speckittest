import { CopyForm } from './components/CopyForm';
import { ProgressDisplay } from './components/ProgressDisplay';
import { ResultDisplay } from './components/ResultDisplay';
import { OverwriteConfirmDialog, ConflictResolution } from './components/OverwriteConfirmDialog';
import { useCopyOperation } from './hooks/useCopyOperation';
import { BlobCopyStatus } from './types';
import './App.css';

/**
 * Main application component for the Blob Copy feature.
 * Orchestrates the copy flow: form -> progress -> result
 */
export function App() {
  const {
    operation,
    isLoading,
    error,
    conflictInfo,
    startOperation,
    cancelOperation,
    reset,
    resolveConflictWithOverwrite,
    resolveConflictWithRename,
    cancelConflict,
  } = useCopyOperation();

  /**
   * Handles conflict resolution when destination exists
   */
  const handleConflictResolve = async (
    resolution: ConflictResolution,
    newName?: string
  ) => {
    if (resolution === 'cancel') {
      cancelConflict();
      return;
    }

    if (resolution === 'overwrite') {
      await resolveConflictWithOverwrite();
    } else if (resolution === 'rename' && newName) {
      await resolveConflictWithRename(newName);
    }
  };

  /**
   * Handles cancel dialog dismiss
   */
  const handleConflictCancel = () => {
    cancelConflict();
  };

  /**
   * Handles retry after failure
   */
  const handleRetry = () => {
    reset();
  };

  /**
   * Handles starting a new copy
   */
  const handleNewCopy = () => {
    reset();
  };

  // Determine current view state
  const isInProgress =
    operation?.status === BlobCopyStatus.Running ||
    operation?.status === BlobCopyStatus.Pending;
  const isCompleted = operation?.status === BlobCopyStatus.Completed;
  const isFailed = operation?.status === BlobCopyStatus.Failed;
  const isCancelled = operation?.status === BlobCopyStatus.Cancelled;
  const showResult = isCompleted || isFailed || isCancelled;

  return (
    <div className="app" data-testid="app">
      <header className="app-header">
        <h1>Azure Blob Copy</h1>
        <p className="app-subtitle">
          Copy blobs between Azure Storage containers with real-time progress tracking
        </p>
      </header>

      <main className="app-main">
        {/* Conflict Resolution Dialog */}
        {conflictInfo && (
          <OverwriteConfirmDialog
            destinationUri={conflictInfo.destinationUri}
            onResolve={handleConflictResolve}
            onCancel={handleConflictCancel}
            isProcessing={isLoading}
          />
        )}

        {/* Copy Form - shown when no operation in progress and no conflict */}
        {!operation && !isLoading && !conflictInfo && (
          <CopyForm />
        )}

        {/* Loading state while starting */}
        {isLoading && !operation && (
          <div className="loading-state" data-testid="loading-state">
            <div className="spinner" />
            <p>Starting copy operation...</p>
          </div>
        )}

        {/* Progress Display - shown during copy */}
        {isInProgress && operation && (
          <div className="progress-section">
            <ProgressDisplay
              bytesTransferred={operation.bytesTransferred}
              totalBytes={operation.totalBytes}
              status={operation.status}
            />
            <button
              type="button"
              className="btn btn-secondary cancel-btn"
              onClick={cancelOperation}
              disabled={isLoading}
              data-testid="cancel-operation-button"
            >
              {isLoading ? 'Cancelling...' : 'Cancel Copy'}
            </button>
          </div>
        )}

        {/* Result Display - shown after completion/failure/cancel */}
        {showResult && operation && (
          <ResultDisplay
            operation={operation}
            error={error}
            onRetry={handleRetry}
            onNewCopy={handleNewCopy}
          />
        )}
      </main>

      <footer className="app-footer">
        <p>Blob Storage Copy Tool v1.0</p>
      </footer>
    </div>
  );
}

export default App;
