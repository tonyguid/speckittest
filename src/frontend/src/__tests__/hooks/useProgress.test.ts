import { describe, it, expect } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useProgress } from '../../hooks/useProgress';
import { BlobCopyStatus } from '../../types';

describe('useProgress', () => {
  describe('with no operation', () => {
    it('should return default values', () => {
      const { result } = renderHook(() => useProgress(null));

      expect(result.current.percentageComplete).toBe(0);
      expect(result.current.bytesTransferred).toBe(0);
      expect(result.current.totalBytes).toBe(0);
      expect(result.current.byteRemaining).toBe(0);
      expect(result.current.transferRateMBps).toBe(0);
      expect(result.current.estimatedTimeRemainingSec).toBe(0);
      expect(result.current.estimatedTimeRemainingLabel).toBe('--:--');
      expect(result.current.isIndeterminate).toBe(true);
    });
  });

  describe('with operation in progress', () => {
    it('should calculate percentage complete', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      const { result } = renderHook(() => useProgress(operation));

      expect(result.current.percentageComplete).toBe(50);
      expect(result.current.bytesTransferred).toBe(500);
      expect(result.current.totalBytes).toBe(1000);
      expect(result.current.byteRemaining).toBe(500);
    });

    it('should handle edge case where totalBytes is 0', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 0,
        totalBytes: 0,
        createdAt: new Date().toISOString(),
      };

      const { result } = renderHook(() => useProgress(operation));

      expect(result.current.percentageComplete).toBe(0);
      expect(result.current.isIndeterminate).toBe(true);
    });

    it('should calculate transfer rate from elapsed time', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 10485760, // 10 MB
        totalBytes: 104857600, // 100 MB
        createdAt: new Date().toISOString(),
      };

      const startTime = Date.now() - 5000; // 5 seconds ago

      const { result } = renderHook(() => useProgress(operation, startTime));

      // 10 MB in ~5 seconds = ~2 MB/s
      expect(result.current.transferRateMBps).toBeGreaterThan(1);
      expect(result.current.transferRateMBps).toBeLessThan(3);
    });

    it('should calculate estimated time remaining', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 52428800, // 50 MB
        totalBytes: 104857600, // 100 MB
        createdAt: new Date().toISOString(),
      };

      const startTime = Date.now() - 5000; // 5 seconds ago

      const { result } = renderHook(() => useProgress(operation, startTime));

      expect(result.current.estimatedTimeRemainingSec).toBeGreaterThan(0);
      expect(result.current.estimatedTimeRemainingLabel).not.toBe('--:--');
    });

    it('should format time remaining as MM:SS', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 10485760, // 10 MB
        totalBytes: 104857600, // 100 MB
        createdAt: new Date().toISOString(),
      };

      const startTime = Date.now() - 5000; // 5 seconds ago

      const { result } = renderHook(() => useProgress(operation, startTime));

      const label = result.current.estimatedTimeRemainingLabel;
      expect(label).toMatch(/^\d+:\d{2}$/); // Match MM:SS or M:SS format
    });

    it('should set isIndeterminate when totalBytes is 0', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 1024,
        totalBytes: 0,
        createdAt: new Date().toISOString(),
      };

      const { result } = renderHook(() => useProgress(operation));

      expect(result.current.isIndeterminate).toBe(true);
    });
  });

  describe('with completed operation', () => {
    it('should show 100% progress', () => {
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

      const { result } = renderHook(() => useProgress(operation));

      expect(result.current.percentageComplete).toBe(100);
      expect(result.current.byteRemaining).toBe(0);
    });
  });

  describe('byte formatting', () => {
    it('should return accurate byte counts', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 1073741824, // 1 GB
        totalBytes: 2147483648, // 2 GB
        createdAt: new Date().toISOString(),
      };

      const { result } = renderHook(() => useProgress(operation));

      expect(result.current.bytesTransferred).toBe(1073741824);
      expect(result.current.totalBytes).toBe(2147483648);
      expect(result.current.byteRemaining).toBe(1073741824);
    });
  });

  describe('memoization', () => {
    it('should recalculate only when dependencies change', () => {
      const operation = {
        id: 'op-123',
        sourceUri: 'https://source',
        destinationUri: 'https://dest',
        status: BlobCopyStatus.Running,
        bytesCopied: 500,
        totalBytes: 1000,
        createdAt: new Date().toISOString(),
      };

      const { result, rerender } = renderHook(
        ({ op, time }) => useProgress(op, time),
        {
          initialProps: { op: operation, time: Date.now() },
        }
      );

      const firstResult = result.current;

      // Rerender with same props
      rerender({ op: operation, time: Date.now() });

      // Should return same object reference (memoized)
      expect(result.current).toBe(firstResult);
    });
  });
});
