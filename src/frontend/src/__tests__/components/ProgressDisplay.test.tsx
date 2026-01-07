import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ProgressDisplay } from '../../components/ProgressDisplay';
import { BlobCopyStatus, BlobCopyOperation } from '../../types';

describe('ProgressDisplay', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should not render when operation is null', () => {
    const { container } = render(
      <ProgressDisplay operation={null} />
    );

    expect(
      container.querySelector('[data-testid="progress-display"]')
    ).not.toBeInTheDocument();
  });

  it('should not render when operation is completed', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Completed,
      progressPercentage: 100,
      bytesTransferred: 1000,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const { container } = render(
      <ProgressDisplay operation={operation} />
    );

    expect(
      container.querySelector('[data-testid="progress-display"]')
    ).not.toBeInTheDocument();
  });

  it('should render when operation is pending', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Pending,
      progressPercentage: 0,
      bytesTransferred: 0,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    expect(
      screen.getByTestId('progress-display')
    ).toBeInTheDocument();
    expect(screen.getByText('⏳ Pending')).toBeInTheDocument();
  });

  it('should render when operation is running', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    expect(
      screen.getByTestId('progress-display')
    ).toBeInTheDocument();
    expect(screen.getByText('⚙️ Running')).toBeInTheDocument();
  });

  it('should display operation ID', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    expect(screen.getByText('op-123')).toBeInTheDocument();
  });

  it('should display progress percentage', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    expect(screen.getByTestId('progress-percentage')).toHaveTextContent('50%');
  });

  it('should display bytes transferred', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 1073741824, // 1 GB
      totalBytes: 2147483648, // 2 GB
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    const bytesEl = screen.getByTestId('bytes-transferred');
    expect(bytesEl.textContent).toContain('1 GB');
    expect(bytesEl.textContent).toContain('2 GB');
  });

  it('should display transfer rate for running operation', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 10,
      bytesTransferred: 10485760, // 10 MB
      totalBytes: 104857600, // 100 MB
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const startTime = Date.now() - 5000; // 5 seconds ago

    render(
      <ProgressDisplay
        operation={operation}
        startTime={startTime}
      />
    );

    expect(screen.getByTestId('transfer-rate')).toBeInTheDocument();
    expect(screen.getByTestId('transfer-rate').textContent).toMatch(/MB\/s/);
  });

  it('should display time remaining for running operation', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 52428800, // 50 MB
      totalBytes: 104857600, // 100 MB
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const startTime = Date.now() - 5000; // 5 seconds ago

    render(
      <ProgressDisplay
        operation={operation}
        startTime={startTime}
      />
    );

    expect(screen.getByTestId('time-remaining')).toBeInTheDocument();
    expect(
      screen.getByTestId('time-remaining').textContent
    ).toMatch(/\d+:\d{2}/);
  });

  it('should show indeterminate progress when total size is unknown', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 0,
      bytesTransferred: 1024,
      totalBytes: 0,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    expect(screen.getByTestId('progress-percentage')).toHaveTextContent(
      'Calculating...'
    );
  });

  it('should render cancel button when running', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const mockOnCancel = vi.fn();

    render(
      <ProgressDisplay
        operation={operation}
        onCancel={mockOnCancel}
      />
    );

    const cancelButton = screen.getByTestId('cancel-button');
    expect(cancelButton).toBeInTheDocument();
    expect(cancelButton.textContent).toContain('Cancel Copy');
  });

  it('should disable cancel button while cancelling', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const mockOnCancel = vi.fn();

    render(
      <ProgressDisplay
        operation={operation}
        onCancel={mockOnCancel}
        isCancelling={true}
      />
    );

    const cancelButton = screen.getByTestId('cancel-button');
    expect(cancelButton).toHaveAttribute('disabled');
    expect(cancelButton.textContent).toContain('Cancelling...');
  });

  it('should not render cancel button when pending', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Pending,
      progressPercentage: 0,
      bytesTransferred: 0,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    const { container } = render(
      <ProgressDisplay operation={operation} />
    );

    expect(
      container.querySelector('[data-testid="cancel-button"]')
    ).not.toBeInTheDocument();
  });

  it('should render progress bar', () => {
    const operation: BlobCopyOperation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      progressPercentage: 50,
      bytesTransferred: 500,
      totalBytes: 1000,
      startedAt: new Date().toISOString(),
      correlationId: 'corr-123',
    };

    render(<ProgressDisplay operation={operation} />);

    const progressBar = screen.getByTestId('progress-bar');
    expect(progressBar).toBeInTheDocument();
  });
});
