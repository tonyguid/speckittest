import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useCopyOperation } from '../../hooks/useCopyOperation';
import { apiClient } from '../../services/apiClient';
import { signalRClient } from '../../services/signalRClient';
import { BlobCopyStatus } from '../../types';

// Mock services
vi.mock('../../services/apiClient');
vi.mock('../../services/signalRClient');

describe('useCopyOperation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('startOperation', () => {
    it('should initialize operation state', async () => {
      const mockOperation = {
        id: 'op-123',
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
        status: BlobCopyStatus.Pending,
        bytesCopied: 0,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      vi.mocked(apiClient.startCopy).mockResolvedValue(mockOperation);
      vi.mocked(signalRClient.isConnected).mockReturnValue(false);
      vi.mocked(signalRClient.connect).mockResolvedValue(undefined);

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          mockOperation.sourceUri,
          mockOperation.destinationUri,
          false
        );
      });

      expect(result.current.operation).toEqual(mockOperation);
      expect(result.current.error).toBeNull();
      expect(result.current.isLoading).toBe(false);
    });

    it('should handle startCopy errors', async () => {
      const error = new Error('Copy failed');
      vi.mocked(apiClient.startCopy).mockRejectedValue(error);

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          'https://source',
          'https://dest',
          false
        );
      });

      expect(result.current.error).toBe('Copy failed');
      expect(result.current.operation).toBeNull();
    });

    it('should connect to SignalR when requested', async () => {
      const mockOperation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Pending,
        bytesCopied: 0,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      vi.mocked(apiClient.startCopy).mockResolvedValue(mockOperation);
      vi.mocked(signalRClient.isConnected).mockReturnValue(false);
      vi.mocked(signalRClient.connect).mockResolvedValue(undefined);

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          mockOperation.sourceUri,
          mockOperation.destinationUri,
          true
        );
      });

      expect(signalRClient.connect).toHaveBeenCalledWith(
        mockOperation.id,
        expect.any(Object)
      );
    });

    it('should fallback to polling on WebSocket error', async () => {
      const mockOperation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Pending,
        bytesCopied: 0,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      vi.mocked(apiClient.startCopy).mockResolvedValue(mockOperation);
      vi.mocked(signalRClient.isConnected).mockReturnValue(false);
      vi.mocked(signalRClient.connect).mockRejectedValue(
        new Error('WebSocket failed')
      );
      vi.mocked(apiClient.getStatus).mockResolvedValue({
        ...mockOperation,
        status: BlobCopyStatus.Running,
      });

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          mockOperation.sourceUri,
          mockOperation.destinationUri,
          true
        );
      });

      // Should poll for status
      await waitFor(() => {
        expect(apiClient.getStatus).toHaveBeenCalled();
      });
    });
  });

  describe('cancelOperation', () => {
    it('should cancel running operation', async () => {
      const mockOperation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      const cancelledOp = {
        ...mockOperation,
        status: BlobCopyStatus.Cancelled,
        completedAt: new Date().toISOString(),
      };

      vi.mocked(apiClient.startCopy).mockResolvedValue(mockOperation);
      vi.mocked(signalRClient.isConnected).mockReturnValue(false);
      vi.mocked(signalRClient.connect).mockResolvedValue(undefined);
      vi.mocked(apiClient.cancelCopy).mockResolvedValue(cancelledOp);
      vi.mocked(signalRClient.unsubscribeFromOperation).mockResolvedValue(
        undefined
      );

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          mockOperation.sourceUri,
          mockOperation.destinationUri,
          false
        );
      });

      await act(async () => {
        await result.current.cancelOperation();
      });

      expect(apiClient.cancelCopy).toHaveBeenCalledWith('op-123');
      expect(result.current.operation?.status).toBe(BlobCopyStatus.Cancelled);
    });

    it('should error if no operation to cancel', async () => {
      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.cancelOperation();
      });

      expect(result.current.error).toBe('No operation to cancel');
    });
  });

  describe('reset', () => {
    it('should clear operation state', async () => {
      const mockOperation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Completed,
        bytesCopied: 1000,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };

      vi.mocked(apiClient.startCopy).mockResolvedValue(mockOperation);
      vi.mocked(signalRClient.isConnected).mockReturnValue(false);
      vi.mocked(signalRClient.connect).mockResolvedValue(undefined);
      vi.mocked(signalRClient.unsubscribeFromOperation).mockResolvedValue(
        undefined
      );

      const { result } = renderHook(() => useCopyOperation());

      await act(async () => {
        await result.current.startOperation(
          mockOperation.sourceUri,
          mockOperation.destinationUri,
          false
        );
      });

      await act(async () => {
        await result.current.reset();
      });

      expect(result.current.operation).toBeNull();
      expect(result.current.error).toBeNull();
    });
  });
});
