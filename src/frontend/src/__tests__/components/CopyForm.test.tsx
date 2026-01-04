import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CopyForm } from '../../components/CopyForm';
import { useCopyOperation } from '../../hooks/useCopyOperation';

// Mock the hook
vi.mock('../../hooks/useCopyOperation');

describe('CopyForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useCopyOperation).mockReturnValue({
      operation: null,
      isLoading: false,
      error: null,
      startOperation: vi.fn().mockResolvedValue(undefined),
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });
  });

  it('should render form with input fields', () => {
    render(<CopyForm />);

    expect(screen.getByTestId('copy-form')).toBeInTheDocument();
    expect(screen.getByTestId('source-uri-input')).toBeInTheDocument();
    expect(screen.getByTestId('destination-uri-input')).toBeInTheDocument();
    expect(screen.getByTestId('start-copy-button')).toBeInTheDocument();
  });

  it('should validate required fields', async () => {
    const user = userEvent.setup();
    render(<CopyForm />);

    const submitButton = screen.getByTestId('start-copy-button');
    await user.click(submitButton);

    const errors = screen.getByTestId('validation-errors');
    expect(errors).toBeInTheDocument();
    expect(errors.textContent).toContain('Source URI is required');
    expect(errors.textContent).toContain('Destination URI is required');
  });

  it('should validate that URIs are different', async () => {
    const user = userEvent.setup();
    render(<CopyForm />);

    const sourceInput = screen.getByTestId('source-uri-input');
    const destInput = screen.getByTestId('destination-uri-input');
    const submitButton = screen.getByTestId('start-copy-button');

    const uri = 'https://account.blob.core.windows.net/container/blob';
    await user.type(sourceInput, uri);
    await user.type(destInput, uri);
    await user.click(submitButton);

    const errors = screen.getByTestId('validation-errors');
    expect(errors.textContent).toContain(
      'Source and destination URIs must be different'
    );
  });

  it('should call startOperation with valid inputs', async () => {
    const user = userEvent.setup();
    const mockStartOperation = vi.fn().mockResolvedValue(undefined);

    vi.mocked(useCopyOperation).mockReturnValue({
      operation: null,
      isLoading: false,
      error: null,
      startOperation: mockStartOperation,
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });

    render(<CopyForm />);

    const sourceInput = screen.getByTestId('source-uri-input');
    const destInput = screen.getByTestId('destination-uri-input');
    const submitButton = screen.getByTestId('start-copy-button');

    const sourceUri = 'https://account.blob.core.windows.net/source/blob';
    const destUri = 'https://account.blob.core.windows.net/dest/blob';

    await user.type(sourceInput, sourceUri);
    await user.type(destInput, destUri);
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockStartOperation).toHaveBeenCalledWith(sourceUri, destUri);
    });
  });

  it('should trim whitespace from URIs', async () => {
    const user = userEvent.setup();
    const mockStartOperation = vi.fn().mockResolvedValue(undefined);

    vi.mocked(useCopyOperation).mockReturnValue({
      operation: null,
      isLoading: false,
      error: null,
      startOperation: mockStartOperation,
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });

    render(<CopyForm />);

    const sourceInput = screen.getByTestId('source-uri-input');
    const destInput = screen.getByTestId('destination-uri-input');
    const submitButton = screen.getByTestId('start-copy-button');

    const sourceUri = '  https://account.blob.core.windows.net/source/blob  ';
    const destUri = '  https://account.blob.core.windows.net/dest/blob  ';

    await user.type(sourceInput, sourceUri);
    await user.type(destInput, destUri);
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockStartOperation).toHaveBeenCalledWith(
        sourceUri.trim(),
        destUri.trim()
      );
    });
  });

  it('should display server errors', () => {
    const serverError = 'Source blob not found';
    vi.mocked(useCopyOperation).mockReturnValue({
      operation: null,
      isLoading: false,
      error: serverError,
      startOperation: vi.fn(),
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });

    render(<CopyForm />);

    const errorDisplay = screen.getByTestId('server-error');
    expect(errorDisplay).toBeInTheDocument();
    expect(errorDisplay.textContent).toContain(serverError);
  });

  it('should disable form while loading', async () => {
    vi.mocked(useCopyOperation).mockReturnValue({
      operation: null,
      isLoading: true,
      error: null,
      startOperation: vi.fn(),
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });

    render(<CopyForm />);

    const fieldset = screen.getByTestId('copy-form').querySelector('fieldset');
    expect(fieldset).toHaveAttribute('disabled');

    const submitButton = screen.getByTestId('start-copy-button');
    expect(submitButton.textContent).toContain('Starting Copy');
  });

  it('should hide form when operation is running', () => {
    const mockOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: 'Running' as const,
      bytesCopied: 0,
      totalBytes: 1000,
      createdAt: new Date().toISOString(),
    };

    vi.mocked(useCopyOperation).mockReturnValue({
      operation: mockOperation,
      isLoading: false,
      error: null,
      startOperation: vi.fn(),
      cancelOperation: vi.fn(),
      reset: vi.fn(),
    });

    const { container } = render(<CopyForm />);

    expect(container.querySelector('[data-testid="copy-form"]')).not.toBeInTheDocument();
  });

  it('should clear validation errors when user changes inputs', async () => {
    const user = userEvent.setup();
    render(<CopyForm />);

    // Trigger validation error
    const submitButton = screen.getByTestId('start-copy-button');
    await user.click(submitButton);

    expect(screen.getByTestId('validation-errors')).toBeInTheDocument();

    // Start typing
    const sourceInput = screen.getByTestId('source-uri-input');
    await user.type(sourceInput, 'https://source');

    // Errors should clear on submit
    await user.click(submitButton);

    // Should show different error (destination required)
    const errors = screen.getByTestId('validation-errors');
    expect(errors.textContent).toContain('Destination URI is required');
  });
});
