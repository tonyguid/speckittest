import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ResultDisplay } from '../../components/ResultDisplay';
import { BlobCopyStatus } from '../../types';

describe('ResultDisplay', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should not render when operation is null', () => {
    const { container } = render(
      <ResultDisplay operation={null} />
    );

    expect(
      container.querySelector('[data-testid="result-display"]')
    ).not.toBeInTheDocument();
  });

  it('should not render when operation is pending', () => {
    const operation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Pending,
      bytesCopied: 0,
      totalBytes: 1000,
      createdAt: new Date().toISOString(),
    };

    const { container } = render(
      <ResultDisplay operation={operation} />
    );

    expect(
      container.querySelector('[data-testid="result-display"]')
    ).not.toBeInTheDocument();
  });

  it('should not render when operation is running', () => {
    const operation = {
      id: 'op-123',
      sourceUri: 'https://source',
      destinationUri: 'https://dest',
      status: BlobCopyStatus.Running,
      bytesCopied: 500,
      totalBytes: 1000,
      createdAt: new Date().toISOString(),
    };

    const { container } = render(
      <ResultDisplay operation={operation} />
    );

    expect(
      container.querySelector('[data-testid="result-display"]')
    ).not.toBeInTheDocument();
  });

  describe('success result', () => {
    it('should display success message', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      expect(
        screen.getByText('✅ Copy Completed Successfully')
      ).toBeInTheDocument();
    });

    it('should display source and destination URIs', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source/blob',
        destinationUri: 'https://dest/blob',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      expect(screen.getByText('https://source/blob')).toBeInTheDocument();
      expect(screen.getByText('https://dest/blob')).toBeInTheDocument();
    });

    it('should display bytes copied and total size', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1073741824, // 1 GB
        totalBytes: 1073741824, // 1 GB
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      const resultDisplay = screen.getByTestId('result-display');
      expect(resultDisplay.textContent).toContain('1 GB');
    });

    it('should display completion duration', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} durationSeconds={120} />);

      expect(screen.getByText(/2m 0s/)).toBeInTheDocument();
    });

    it('should display completion timestamp', () => {
      const now = new Date();
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: now.toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      expect(screen.getByText(/Completed At:/)).toBeInTheDocument();
    });
  });

  describe('failure result', () => {
    it('should display failure message', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Failed,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        errors: [
          {
            field: 'sourceUri',
            message: 'Source blob not found',
          },
        ],
      };

      render(<ResultDisplay operation={operation} />);

      expect(screen.getByText('❌ Copy Failed')).toBeInTheDocument();
    });

    it('should display error list', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Failed,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        errors: [
          {
            field: 'sourceUri',
            message: 'Source blob not found',
          },
          {
            field: 'permissions',
            message: 'Access denied to source container',
          },
        ],
      };

      render(<ResultDisplay operation={operation} />);

      const errorList = screen.getByTestId('error-list');
      expect(errorList).toBeInTheDocument();
      expect(errorList.textContent).toContain('Source blob not found');
      expect(errorList.textContent).toContain('Access denied to source container');
      expect(errorList.textContent).toContain('Field: sourceUri');
      expect(errorList.textContent).toContain('Field: permissions');
    });

    it('should display URIs and duration before failure', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source/blob',
        destinationUri: 'https://dest/blob',
        status: BlobCopyStatus.Failed,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        errors: [
          {
            field: 'sourceUri',
            message: 'Source blob not found',
          },
        ],
      };

      render(<ResultDisplay operation={operation} durationSeconds={30} />);

      expect(screen.getByText('https://source/blob')).toBeInTheDocument();
      expect(screen.getByText('https://dest/blob')).toBeInTheDocument();
      expect(screen.getByText(/30s/)).toBeInTheDocument();
    });
  });

  describe('cancelled result', () => {
    it('should display cancellation message', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Cancelled,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      expect(screen.getByText('⚠️ Copy Cancelled')).toBeInTheDocument();
    });

    it('should display bytes copied before cancellation', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Cancelled,
        bytesCopied: 536870912, // 512 MB
        totalBytes: 1073741824, // 1 GB
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      const resultDisplay = screen.getByTestId('result-display');
      expect(resultDisplay.textContent).toContain('512 MB');
      expect(resultDisplay.textContent).toContain('1 GB');
    });

    it('should display cancellation timestamp and duration', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Cancelled,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} durationSeconds={45} />);

      expect(screen.getByText(/Cancelled At:/)).toBeInTheDocument();
      expect(screen.getByText(/45s/)).toBeInTheDocument();
    });
  });

  describe('byte formatting', () => {
    it('should format large file sizes correctly', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1099511627776, // 1 TB
        totalBytes: 1099511627776,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      expect(screen.getByText(/1 TB/)).toBeInTheDocument();
    });
  });

  describe('duration formatting', () => {
    it('should format duration as HH:MM:SS', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} durationSeconds={3665} />);

      expect(screen.getByText(/1h 1m 5s/)).toBeInTheDocument();
    });
  });

  describe('result styling', () => {
    it('should apply success class', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      const resultDisplay = screen.getByTestId('result-display');
      expect(resultDisplay).toHaveClass('result-completed');
    });

    it('should apply failure class', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Failed,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        errors: [],
      };

      render(<ResultDisplay operation={operation} />);

      const resultDisplay = screen.getByTestId('result-display');
      expect(resultDisplay).toHaveClass('result-failed');
    });

    it('should apply cancelled class', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Cancelled,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      render(<ResultDisplay operation={operation} />);

      const resultDisplay = screen.getByTestId('result-display');
      expect(resultDisplay).toHaveClass('result-cancelled');
    });
  });
});
