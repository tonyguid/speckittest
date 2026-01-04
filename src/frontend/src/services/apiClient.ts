import axios, { AxiosInstance } from 'axios';
import { CopyRequest, BlobCopyOperation, ValidationError } from '../types';

/**
 * API client for blob copy backend service.
 * Handles HTTP communication with all backend endpoints.
 */
export class ApiClient {
  private client: AxiosInstance;
  private baseURL: string;

  constructor(baseURL: string = '/api') {
    this.baseURL = baseURL;
    this.client = axios.create({
      baseURL: this.baseURL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });
  }

  /**
   * Validates source and destination blob URIs without starting copy.
   *
   * @param request - Copy request with source and destination URIs
   * @returns Array of validation errors (empty if valid)
   * @throws Error if validation request fails
   */
  async validate(request: CopyRequest): Promise<ValidationError[]> {
    try {
      const response = await this.client.post<ValidationError[]>(
        '/blobcopy/validate',
        request
      );
      return response.data || [];
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 400) {
        // Validation errors returned as 400
        return (error.response.data as ValidationError[]) || [];
      }
      throw this.handleError(error, 'Validation failed');
    }
  }

  /**
   * Starts a new blob copy operation.
   *
   * @param request - Copy request with source and destination URIs
   * @returns Created BlobCopyOperation with operation ID
   * @throws Error if copy operation cannot be started
   */
  async startCopy(request: CopyRequest): Promise<BlobCopyOperation> {
    try {
      const response = await this.client.post<BlobCopyOperation>(
        '/blobcopy/start',
        request
      );
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 400) {
        const errors = error.response.data as ValidationError[];
        throw new Error(
          `Cannot start copy: ${errors[0]?.message || 'Validation failed'}`
        );
      }
      throw this.handleError(error, 'Failed to start copy operation');
    }
  }

  /**
   * Gets the current status of a copy operation.
   *
   * @param operationId - Operation ID to check status for
   * @returns Current BlobCopyOperation status
   * @throws Error if operation not found or status cannot be retrieved
   */
  async getStatus(operationId: string): Promise<BlobCopyOperation> {
    try {
      const response = await this.client.get<BlobCopyOperation>(
        `/blobcopy/status/${operationId}`
      );
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        throw new Error(`Operation '${operationId}' not found`);
      }
      throw this.handleError(error, 'Failed to retrieve operation status');
    }
  }

  /**
   * Cancels a copy operation that is in progress or pending.
   *
   * @param operationId - Operation ID to cancel
   * @returns Updated BlobCopyOperation with cancelled status
   * @throws Error if operation not found or cannot be cancelled
   */
  async cancelCopy(operationId: string): Promise<BlobCopyOperation> {
    try {
      const response = await this.client.post<BlobCopyOperation>(
        `/blobcopy/cancel/${operationId}`
      );
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        throw new Error(`Operation '${operationId}' not found`);
      }
      throw this.handleError(error, 'Failed to cancel operation');
    }
  }

  /**
   * Checks service health status.
   *
   * @returns True if service is healthy
   */
  async checkHealth(): Promise<boolean> {
    try {
      await this.client.get('/blobcopy/health');
      return true;
    } catch (error) {
      console.error('Health check failed:', error);
      return false;
    }
  }

  /**
   * Sets the API base URL (useful for testing or multi-environment deployments).
   *
   * @param baseURL - New base URL
   */
  setBaseURL(baseURL: string): void {
    this.baseURL = baseURL;
    this.client.defaults.baseURL = baseURL;
  }

  /**
   * Sets an authorization token in API requests.
   *
   * @param token - JWT or other auth token
   */
  setAuthToken(token: string): void {
    this.client.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }

  /**
   * Handles and normalizes API errors.
   *
   * @param error - Error from axios
   * @param defaultMessage - Default error message if none provided
   * @returns Normalized error
   */
  private handleError(error: any, defaultMessage: string): Error {
    if (axios.isAxiosError(error)) {
      const message = error.response?.data?.message || 
                     error.message || 
                     defaultMessage;
      return new Error(message);
    }
    return error instanceof Error ? error : new Error(defaultMessage);
  }
}

// Export singleton instance
export const apiClient = new ApiClient();
