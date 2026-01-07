/**
 * Type definitions for the Blob Copy feature
 */

export enum BlobCopyStatus {
  Pending = 'Pending',
  Running = 'Running',
  Completed = 'Completed',
  Failed = 'Failed',
  Cancelled = 'Cancelled',
}

export interface BlobCopyOperation {
  id: string;
  sourceUri: string;
  destinationUri: string;
  status: BlobCopyStatus;
  progressPercentage: number;
  bytesTransferred: number;
  totalBytes: number;
  estimatedTimeRemaining?: number;
  errorMessage?: string;
  errorCode?: string;
  startedAt: string; // ISO 8601
  completedAt?: string; // ISO 8601
  correlationId: string;
}

export interface ProgressUpdate {
  copyOperationId: string;
  bytesTransferred: number;
  totalBytes: number;
  progressPercentage: number;
  estimatedSecondsRemaining?: number;
  updatedAt: string; // ISO 8601
  transferRateMbps: number;
}

export interface ValidationError {
  field: string;
  message: string;
  code?: string;
}

export interface CopyRequest {
  sourceUri: string;
  destinationUri: string;
}
