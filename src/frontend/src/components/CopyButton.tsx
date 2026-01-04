import React from 'react';
import './CopyButton.css';

/**
 * Props for the CopyButton component
 */
export interface CopyButtonProps {
  /** Button click handler */
  onClick: () => void;
  /** Whether the button is disabled */
  disabled?: boolean;
  /** Whether the button is in loading state */
  isLoading?: boolean;
  /** Optional tooltip text to display on hover */
  tooltip?: string;
  /** Optional custom button text (default: "Copy Blob") */
  text?: string;
}

/**
 * CopyButton Component
 * 
 * Reusable button component for initiating blob copy operations.
 * Supports loading state with spinner, disabled state, and optional tooltip.
 * 
 * @example
 * ```tsx
 * <CopyButton 
 *   onClick={handleCopy}
 *   disabled={!isValid}
 *   isLoading={copying}
 *   tooltip="Start copying the blob"
 * />
 * ```
 */
export const CopyButton: React.FC<CopyButtonProps> = ({
  onClick,
  disabled = false,
  isLoading = false,
  tooltip,
  text = 'Copy Blob'
}) => {
  const isDisabled = disabled || isLoading;

  const handleClick = () => {
    if (!isDisabled) {
      onClick();
    }
  };

  return (
    <div className="copy-button-wrapper" title={tooltip}>
      <button
        className={`copy-button ${isLoading ? 'loading' : ''} ${isDisabled ? 'disabled' : ''}`}
        onClick={handleClick}
        disabled={isDisabled}
        data-testid="start-copy-button"
        aria-busy={isLoading}
        aria-disabled={isDisabled}
      >
        {isLoading && (
          <span className="spinner" aria-label="Loading">
            <svg
              className="spinner-icon"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              width="16"
              height="16"
            >
              <circle
                className="spinner-circle"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="spinner-path"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
          </span>
        )}
        <span className="button-text">{isLoading ? 'Copying...' : text}</span>
      </button>
    </div>
  );
};

export default CopyButton;
