import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OverwriteConfirmDialog } from '../../components/OverwriteConfirmDialog';

describe('OverwriteConfirmDialog', () => {
  const defaultProps = {
    destinationUri: 'https://account.blob.core.windows.net/container/existing-blob.txt',
    onResolve: vi.fn(),
    onCancel: vi.fn(),
    isProcessing: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the dialog with destination URI', () => {
    render(<OverwriteConfirmDialog {...defaultProps} />);

    expect(screen.getByTestId('overwrite-confirm-dialog')).toBeInTheDocument();
    expect(screen.getByText('Destination Blob Already Exists')).toBeInTheDocument();
    expect(screen.getByText(/existing-blob.txt/)).toBeInTheDocument();
  });

  it('calls onResolve with "overwrite" when overwrite button is clicked', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    await user.click(screen.getByTestId('overwrite-button'));

    expect(defaultProps.onResolve).toHaveBeenCalledWith('overwrite');
    expect(defaultProps.onResolve).toHaveBeenCalledTimes(1);
  });

  it('calls onResolve with "cancel" and onCancel when cancel button is clicked', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    await user.click(screen.getByTestId('cancel-button'));

    expect(defaultProps.onResolve).toHaveBeenCalledWith('cancel');
    expect(defaultProps.onCancel).toHaveBeenCalled();
  });

  it('shows rename input when rename button is clicked', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    // Initially, rename input should not be visible
    expect(screen.queryByTestId('rename-input')).not.toBeInTheDocument();

    // Click rename button
    await user.click(screen.getByTestId('rename-button'));

    // Rename input should now be visible
    expect(screen.getByTestId('rename-input')).toBeInTheDocument();
    expect(screen.getByTestId('rename-submit-button')).toBeInTheDocument();
    expect(screen.getByTestId('rename-cancel-button')).toBeInTheDocument();
  });

  it('calls onResolve with "rename" and new name when rename is submitted', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    // Click rename button
    await user.click(screen.getByTestId('rename-button'));

    // Clear and type new name
    const input = screen.getByTestId('rename-input');
    await user.clear(input);
    await user.type(input, 'new-blob-name.txt');

    // Submit
    await user.click(screen.getByTestId('rename-submit-button'));

    expect(defaultProps.onResolve).toHaveBeenCalledWith('rename', 'new-blob-name.txt');
  });

  it('shows error when rename is submitted with empty name', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    await user.click(screen.getByTestId('rename-button'));

    const input = screen.getByTestId('rename-input');
    await user.clear(input);

    await user.click(screen.getByTestId('rename-submit-button'));

    expect(screen.getByText('Please enter a valid name')).toBeInTheDocument();
    expect(defaultProps.onResolve).not.toHaveBeenCalled();
  });

  it('shows error when rename is submitted with same name', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    await user.click(screen.getByTestId('rename-button'));

    // The input is pre-filled with the original name
    await user.click(screen.getByTestId('rename-submit-button'));

    expect(screen.getByText('New name must be different from the original')).toBeInTheDocument();
    expect(defaultProps.onResolve).not.toHaveBeenCalled();
  });

  it('hides rename input when back button is clicked', async () => {
    const user = userEvent.setup();
    render(<OverwriteConfirmDialog {...defaultProps} />);

    await user.click(screen.getByTestId('rename-button'));
    expect(screen.getByTestId('rename-input')).toBeInTheDocument();

    await user.click(screen.getByTestId('rename-cancel-button'));
    expect(screen.queryByTestId('rename-input')).not.toBeInTheDocument();
  });

  it('disables buttons when isProcessing is true', () => {
    render(<OverwriteConfirmDialog {...defaultProps} isProcessing={true} />);

    expect(screen.getByTestId('overwrite-button')).toBeDisabled();
    expect(screen.getByTestId('rename-button')).toBeDisabled();
    expect(screen.getByTestId('cancel-button')).toBeDisabled();
  });

  it('shows "Processing..." text when isProcessing is true', () => {
    render(<OverwriteConfirmDialog {...defaultProps} isProcessing={true} />);

    expect(screen.getByTestId('overwrite-button')).toHaveTextContent('Processing...');
  });

  it('truncates long URIs in display', () => {
    const longUri = 'https://verylongstorageaccountname.blob.core.windows.net/container/path/to/very/long/blob/name.txt';
    render(<OverwriteConfirmDialog {...defaultProps} destinationUri={longUri} />);

    // Should show truncated URI with ellipsis
    const uriElement = screen.getByTitle(longUri);
    expect(uriElement).toBeInTheDocument();
    expect(uriElement.textContent).toContain('...');
  });

  it('has proper accessibility attributes', () => {
    render(<OverwriteConfirmDialog {...defaultProps} />);

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAttribute('aria-labelledby', 'conflict-dialog-title');
  });
});
