import { useState } from 'react';
import { useCopyOperation } from '../hooks/useCopyOperation';
import './CopyForm.css';

/**
 * Form component for initiating a blob copy operation.
 * Collects source and destination URIs, validates them, and starts the copy.
 */
export function CopyForm() {
  const [sourceUri, setSourceUri] = useState('');
  const [destinationUri, setDestinationUri] = useState('');
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [isValidating, setIsValidating] = useState(false);

  const { operation, isLoading, error, startOperation } = useCopyOperation();

  /**
   * Handles form submission - validates and starts copy operation.
   */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setValidationErrors([]);

    // Basic client-side validation
    const errors: string[] = [];

    if (!sourceUri.trim()) {
      errors.push('Source URI is required');
    }
    if (!destinationUri.trim()) {
      errors.push('Destination URI is required');
    }
    if (sourceUri === destinationUri) {
      errors.push('Source and destination URIs must be different');
    }

    if (errors.length > 0) {
      setValidationErrors(errors);
      return;
    }

    // Start the copy operation (which will perform server-side validation)
    await startOperation(sourceUri.trim(), destinationUri.trim());
  };

  // Don't show form if operation is in progress or completed
  if (operation) {
    return null;
  }

  return (
    <form onSubmit={handleSubmit} className="copy-form" data-testid="copy-form">
      <fieldset disabled={isLoading || isValidating}>
        <legend>Blob Copy Operation</legend>

        {/* Validation Errors */}
        {validationErrors.length > 0 && (
          <div className="form-errors" role="alert" data-testid="validation-errors">
            {validationErrors.map((err, idx) => (
              <p key={idx} className="error-message">
                ⚠️ {err}
              </p>
            ))}
          </div>
        )}

        {/* Server Error */}
        {error && (
          <div className="form-error" role="alert" data-testid="server-error">
            <p className="error-message">❌ {error}</p>
          </div>
        )}

        {/* Source URI Input */}
        <div className="form-group">
          <label htmlFor="sourceUri">Source Blob URI:</label>
          <input
            id="sourceUri"
            type="text"
            placeholder="https://account.blob.core.windows.net/container/blob"
            value={sourceUri}
            onChange={(e) => setSourceUri(e.target.value)}
            className="form-input"
            data-testid="source-uri-input"
            required
          />
          <p className="input-hint">
            The full URI of the blob to copy (e.g.,
            https://myaccount.blob.core.windows.net/source/file.vhd)
          </p>
        </div>

        {/* Destination URI Input */}
        <div className="form-group">
          <label htmlFor="destinationUri">Destination Blob URI:</label>
          <input
            id="destinationUri"
            type="text"
            placeholder="https://account.blob.core.windows.net/container/blob"
            value={destinationUri}
            onChange={(e) => setDestinationUri(e.target.value)}
            className="form-input"
            data-testid="destination-uri-input"
            required
          />
          <p className="input-hint">
            The full URI where the blob will be copied to (e.g.,
            https://myaccount.blob.core.windows.net/destination/file-copy.vhd)
          </p>
        </div>

        {/* Submit Button */}
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isLoading || isValidating}
          data-testid="start-copy-button"
        >
          {isLoading || isValidating ? 'Starting Copy...' : 'Start Copy'}
        </button>
      </fieldset>
    </form>
  );
}
