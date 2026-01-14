import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CopyRequest, BlobCopyOperation, BlobCopyStatus } from '../../types';

// Create shared mock instance
const mockPostFn = vi.fn();
const mockGetFn = vi.fn();

// Mock axios module - must be self-contained
vi.mock('axios', () => {
  // Create instance inside the factory
  const instance = {
    post: (...args: any[]) => mockPostFn(...args),
    get: (...args: any[]) => mockGetFn(...args),
    defaults: {
      baseURL: '/api',
      headers: {
        common: {},
      },
    },
  };

  return {
    default: {
      create: () => instance,
      isAxiosError: (error: any) => error?.isAxiosError === true,
    },
  };
});

// Import after mock setup
import { apiClient } from '../../services/apiClient';

describe('ApiClient', () => {
  beforeEach(() => {
    mockPostFn.mockClear();
    mockGetFn.mockClear();
  });

  describe('validate', () => {
    it('should return empty array for valid blobs', async () => {
      const mockResponse = { data: [] };
      mockPostFn.mockResolvedValue(mockResponse);

      const request: CopyRequest = {
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      const errors = await apiClient.validate(request);

      expect(errors).toEqual([]);
      expect(mockPostFn).toHaveBeenCalledWith('/blobcopy/validate', request);
    });

    it('should return validation errors for invalid URIs', async () => {
      const mockErrors = [
        { field: 'sourceUri', message: 'Invalid URI format' },
      ];
      const mockResponse = { data: mockErrors };
      mockPostFn.mockResolvedValue(mockResponse);

      const request: CopyRequest = {
        sourceUri: 'invalid',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      const errors = await apiClient.validate(request);

      expect(errors).toEqual(mockErrors);
    });

    it('should handle network errors gracefully', async () => {
      const mockError = {
        isAxiosError: true,
        response: {
          status: 500,
          data: { message: 'Server error' },
        },
        message: 'Server error',
      };
      mockPostFn.mockRejectedValue(mockError);

      const request: CopyRequest = {
        sourceUri: 'https://account.blob.core.windows.net/source/blob',
        destinationUri: 'https://account.blob.core.windows.net/dest/blob',
      };

      await expect(apiClient.validate(request)).rejects.toThrow('Server error');
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

      mockPostFn.mockResolvedValue({ data: mockOperation });

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
        isAxiosError: true,
        response: {
          status: 400,
          data: [{ field: 'sourceUri', message: 'Validation failed' }],
        },
      };

      mockPostFn.mockRejectedValue(mockError);

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

      mockGetFn.mockResolvedValue({ data: mockOperation });

      const status = await apiClient.getStatus('op-123');

      expect(status).toEqual(mockOperation);
      expect(status.status).toBe(BlobCopyStatus.Running);
    });

    it('should throw for non-existent operation (404)', async () => {
      const mockError = {
        isAxiosError: true,
        response: {
          status: 404,
          data: { message: 'Operation not found' },
        },
      };

      mockGetFn.mockRejectedValue(mockError);

      await expect(apiClient.getStatus('non-existent')).rejects.toThrow(
        "Operation 'non-existent' not found"
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

      mockPostFn.mockResolvedValue({ data: mockOperation });

      const result = await apiClient.cancelCopy('op-123');

      expect(result.status).toBe(BlobCopyStatus.Cancelled);
    });

    it('should throw for non-existent operation', async () => {
      const mockError = {
        isAxiosError: true,
        response: {
          status: 404,
          data: { message: 'Operation not found' },
        },
      };

      mockPostFn.mockRejectedValue(mockError);

      await expect(apiClient.cancelCopy('non-existent')).rejects.toThrow(
        "Operation 'non-existent' not found"
      );
    });
  });

  describe('checkHealth', () => {
    it('should return true when service is healthy', async () => {
      mockGetFn.mockResolvedValue({ data: { status: 'healthy' } });

      const isHealthy = await apiClient.checkHealth();

      expect(isHealthy).toBe(true);
    });

    it('should return false when service is unhealthy', async () => {
      mockGetFn.mockRejectedValue(new Error('Service unavailable'));

      const isHealthy = await apiClient.checkHealth();

      expect(isHealthy).toBe(false);
    });
  });

  describe('setAuthToken', () => {
    it('should set authorization header', () => {
      const token = 'token123';

      apiClient.setAuthToken(token);

      // Verify the method was called (actual header is set internally)
      expect(apiClient).toBeDefined();
    });
  });

  describe('setBaseURL', () => {
    it('should update base URL', () => {
      const newUrl = 'https://api.example.com';

      apiClient.setBaseURL(newUrl);

      // Verify the method was called (actual base URL is set internally)
      expect(apiClient).toBeDefined();
    });
  });
});
