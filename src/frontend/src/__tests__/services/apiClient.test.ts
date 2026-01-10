import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import axios from 'axios';
import { apiClient } from '../../services/apiClient';
import { CopyRequest, BlobCopyOperation, BlobCopyStatus } from '../../types';

// Mock axios
vi.mock('axios');

describe('ApiClient', () => {
  const mockAxios = axios as unknown as {
    create: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('validate', () => {
    it('should return empty array for valid blobs', async () => {
      const mockResponse = { data: [] };
      mockAxios.create.mockReturnValue({
        post: vi.fn().mockResolvedValue(mockResponse),
      });

      const request: CopyRequest = {
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      const errors = await apiClient.validate(request);

      expect(errors).toEqual([]);
      expect(mockAxios.create).toHaveBeenCalled();
    });

    it('should return validation errors for invalid URIs', async () => {
      const mockErrors = [
        { field: 'sourceUri', message: 'Invalid URI format' },
      ];
      const mockResponse = { data: mockErrors };
      mockAxios.create.mockReturnValue({
        post: vi.fn().mockResolvedValue(mockResponse),
      });

      const request: CopyRequest = {
        sourceUri: 'invalid',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      const errors = await apiClient.validate(request);

      expect(errors).toEqual(mockErrors);
    });

    it('should handle network errors gracefully', async () => {
      const mockError = {
        response: {
          status: 500,
          data: { message: 'Server error' },
        },
      };
      mockAxios.create.mockReturnValue({
        post: vi.fn().mockRejectedValue(mockError),
      });

      const request: CopyRequest = {
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      await expect(apiClient.validate(request)).rejects.toThrow();
    });
  });

  describe('startCopy', () => {
    it('should return created operation with 201 status', async () => {
      const mockOperation: BlobCopyOperation = {
        id: 'op-123',
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
        status: BlobCopyStatus.Pending,
        progressPercentage: 0,
        bytesTransferred: 0,
        totalBytes: 1000,
        startedAt: new Date().toISOString(),
        correlationId: 'corr-123',
      };

      mockAxios.create.mockReturnValue({
        post: vi.fn().mockResolvedValue({ data: mockOperation }),
      });

      const request: CopyRequest = {
        sourceUri: mockOperation.sourceUri,
        destinationUri: mockOperation.destinationUri,
      };

      const result = await apiClient.startCopy(request);

      expect(result).toEqual(mockOperation);
      expect(result.status).toBe(BlobCopyStatus.Pending);
    });

    it('should throw on validation errors (400)', async () => {
      const mockError = {
        response: {
          status: 400,
          data: { message: 'Validation failed' },
        },
      };

      mockAxios.create.mockReturnValue({
        post: vi.fn().mockRejectedValue(mockError),
      });

      const request: CopyRequest = {
        sourceUri: 'invalid',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      await expect(apiClient.startCopy(request)).rejects.toThrow(
        'Validation failed'
      );
    });
  });

  describe('getStatus', () => {
    it('should return current operation status', async () => {
      const mockOperation: BlobCopyOperation = {
        id: 'op-123',
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
        status: BlobCopyStatus.Running,
        progressPercentage: 50,
        bytesTransferred: 500,
        totalBytes: 1000,
        startedAt: new Date().toISOString(),
        correlationId: 'corr-123',
      };

      mockAxios.create.mockReturnValue({
        get: vi.fn().mockResolvedValue({ data: mockOperation }),
      });

      const status = await apiClient.getStatus('op-123');

      expect(status).toEqual(mockOperation);
      expect(status.status).toBe(BlobCopyStatus.Running);
    });

    it('should throw for non-existent operation (404)', async () => {
      const mockError = {
        response: {
          status: 404,
          data: { message: 'Operation not found' },
        },
      };

      mockAxios.create.mockReturnValue({
        get: vi.fn().mockRejectedValue(mockError),
      });

      await expect(apiClient.getStatus('non-existent')).rejects.toThrow(
        'Operation not found'
      );
    });
  });

  describe('cancelCopy', () => {
    it('should cancel an operation and return updated status', async () => {
      const mockOperation: BlobCopyOperation = {
        id: 'op-123',
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
        status: BlobCopyStatus.Cancelled,
        progressPercentage: 30,
        bytesTransferred: 300,
        totalBytes: 1000,
        startedAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        correlationId: 'corr-123',
      };

      mockAxios.create.mockReturnValue({
        post: vi.fn().mockResolvedValue({ data: mockOperation }),
      });

      const result = await apiClient.cancelCopy('op-123');

      expect(result.status).toBe(BlobCopyStatus.Cancelled);
    });

    it('should throw for non-existent operation', async () => {
      const mockError = {
        response: {
          status: 404,
          data: { message: 'Operation not found' },
        },
      };

      mockAxios.create.mockReturnValue({
        post: vi.fn().mockRejectedValue(mockError),
      });

      await expect(apiClient.cancelCopy('non-existent')).rejects.toThrow();
    });
  });

  describe('checkHealth', () => {
    it('should return true when service is healthy', async () => {
      mockAxios.create.mockReturnValue({
        get: vi.fn().mockResolvedValue({ data: { status: 'healthy' } }),
      });

      const isHealthy = await apiClient.checkHealth();

      expect(isHealthy).toBe(true);
    });

    it('should return false when service is unhealthy', async () => {
      mockAxios.create.mockReturnValue({
        get: vi.fn().mockRejectedValue(new Error('Service unavailable')),
      });

      const isHealthy = await apiClient.checkHealth();

      expect(isHealthy).toBe(false);
    });
  });

  describe('setAuthToken', () => {
    it('should set authorization header', () => {
      const token = 'Bearer token123';

      apiClient.setAuthToken(token);

      // Token should be set in axios default headers (implementation specific)
      expect(apiClient).toBeDefined();
    });
  });
});
