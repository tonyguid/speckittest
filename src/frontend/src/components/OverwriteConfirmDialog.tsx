import { useState } from 'react';
import './OverwriteConfirmDialog.css';

export type ConflictResolution = 'overwrite' | 'cancel' | 'rename';

export interface OverwriteConfirmDialogProps {
  /** The destination URI that already exists */
  destinationUri: string;
  /** Called when user selects an option */
  onResolve: (resolution: ConflictResolution, newName?: string) => void;
  /** Called when dialog is dismissed without selection */
  onCancel: () => void;
  /** Whether the dialog is currently processing */
  isProcessing?: boolean;
}

/**
 * Dialog component for handling destination blob conflicts.
 * Displays when the destination blob already exists and offers:
 * - Overwrite: Replace the existing blob
 * - Cancel: Abort the copy operation
 * - Rename: Provide a new destination name
 */
export function OverwriteConfirmDialog({
  destinationUri,
  onResolve,
  onCancel,
  isProcessing = false,
}: OverwriteConfirmDialogProps) {
  const [showRenameInput, setShowRenameInput] = useState(false);
  const [newName, setNewName] = useState('');
  const [renameError, setRenameError] = useState('');

  const blobName = destinationUri.split('/').pop() || 'blob';

  const handleOverwrite = () => {
    onResolve('overwrite');
  };

  const handleCancel = () => {
    onResolve('cancel');
    onCancel();
  };

  const handleRenameClick = () => {
    setShowRenameInput(true);
    setNewName(blobName);
  };

  const handleRenameSubmit = () => {
    if (!newName.trim()) {
      setRenameError('Please enter a valid name');
      return;
    }
    if (newName === blobName) {
      setRenameError('New name must be different from the original');
      return;
    }
    setRenameError('');
    onResolve('rename', newName.trim());
  };

  const handleRenameCancel = () => {
    setShowRenameInput(false);
    setNewName('');
    setRenameError('');
  };

  return (
    <div
      className="dialog-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="conflict-dialog-title"
      data-testid="overwrite-confirm-dialog"
    >
      <div className="dialog-content">
        <h2 id="conflict-dialog-title" className="dialog-title">
          Destination Blob Already Exists
        </h2>

        <p className="dialog-message">
          A blob already exists at the destination:
        </p>
        <p className="dialog-uri" title={destinationUri}>
          {destinationUri.length > 60
            ? `...${destinationUri.slice(-57)}`
            : destinationUri}
        </p>

        <p className="dialog-question">What would you like to do?</p>

        {!showRenameInput ? (
          <div className="dialog-actions">
            <button
              type="button"
              className="btn btn-danger"
              onClick={handleOverwrite}
              disabled={isProcessing}
              data-testid="overwrite-button"
            >
              {isProcessing ? 'Processing...' : 'Overwrite'}
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleRenameClick}
              disabled={isProcessing}
              data-testid="rename-button"
            >
              Rename
            </button>
            <button
              type="button"
              className="btn btn-outline"
              onClick={handleCancel}
              disabled={isProcessing}
              data-testid="cancel-button"
            >
              Cancel
            </button>
          </div>
        ) : (
          <div className="rename-section">
            <label htmlFor="new-blob-name" className="rename-label">
              Enter new destination name:
            </label>
            <input
              id="new-blob-name"
              type="text"
              className="rename-input"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="new-blob-name.ext"
              disabled={isProcessing}
              data-testid="rename-input"
            />
            {renameError && (
              <p className="rename-error" role="alert">
                {renameError}
              </p>
            )}
            <div className="dialog-actions">
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleRenameSubmit}
                disabled={isProcessing || !newName.trim()}
                data-testid="rename-submit-button"
              >
                {isProcessing ? 'Processing...' : 'Copy with New Name'}
              </button>
              <button
                type="button"
                className="btn btn-outline"
                onClick={handleRenameCancel}
                disabled={isProcessing}
                data-testid="rename-cancel-button"
              >
                Back
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
