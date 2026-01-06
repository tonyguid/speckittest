import { ApplicationInsights } from '@microsoft/applicationinsights-web';
import type { BlobCopyOperation, ProgressUpdate } from '../types';

/**
 * Service for integrating Azure Application Insights with the frontend.
 * Tracks page views, custom events, errors, and performance metrics.
 */
class ApplicationInsightsService {
  private appInsights: ApplicationInsights | null = null;
  private instrumentationKey: string | null = null;

  /**
   * Initialize Application Insights with instrumentation key.
   * @param instrumentationKey - Azure Application Insights instrumentation key
   */
  public initialize(instrumentationKey: string): void {
    if (!instrumentationKey) {
      console.warn('Application Insights instrumentation key not provided');
      return;
    }

    this.instrumentationKey = instrumentationKey;

    this.appInsights = new ApplicationInsights({
      config: {
        instrumentationKey: instrumentationKey,
        enableAutoRouteTracking: true,
        enableRequestHeaderTracking: true,
        enableResponseHeaderTracking: true,
        disableFetchTracking: false,
        disableExceptionTracking: false,
        disableAjaxTracking: false,
        maxBatchSize: 25,
        maxBatchInterval: 15000,
      },
    });

    this.appInsights.loadAppInsights();
    this.appInsights.trackPageView();

    // Track uncaught exceptions
    window.addEventListener('error', (event) => {
      this.trackException(event.error);
    });

    // Track unhandled promise rejections
    window.addEventListener('unhandledrejection', (event) => {
      this.trackException(event.reason);
    });

    console.log('Application Insights initialized');
  }

  /**
   * Track blob copy operation started event.
   */
  public trackCopyOperationStarted(
    operationId: string,
    sourceUri: string,
    destinationUri: string,
    totalBytes: number
  ): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyStarted',
        properties: {
          operationId,
          sourceUri: this.maskUri(sourceUri),
          destinationUri: this.maskUri(destinationUri),
        },
        measurements: {
          totalBytes,
        },
      }
    );
  }

  /**
   * Track blob copy progress update event.
   */
  public trackProgressUpdate(update: ProgressUpdate): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyProgress',
        properties: {
          operationId: update.CopyOperationId,
        },
        measurements: {
          bytesTransferred: update.BytesTransferred,
          totalBytes: update.TotalBytes,
          progressPercentage: update.ProgressPercentage,
          transferRateMbps: update.TransferRateMbps || 0,
          estimatedSecondsRemaining: update.EstimatedSecondsRemaining || 0,
        },
      }
    );
  }

  /**
   * Track blob copy operation completed successfully.
   */
  public trackCopyOperationCompleted(
    operation: BlobCopyOperation,
    durationSeconds: number
  ): void {
    if (!this.appInsights) return;

    const throughputMbps = this.calculateThroughput(operation.BytesCopied || 0, durationSeconds);

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyCompleted',
        properties: {
          operationId: operation.Id,
          sourceUri: this.maskUri(operation.SourceUri),
          destinationUri: this.maskUri(operation.DestinationUri),
        },
        measurements: {
          bytesCopied: operation.BytesCopied || 0,
          totalBytes: operation.TotalBytes,
          durationSeconds,
          throughputMbps,
        },
      }
    );
  }

  /**
   * Track blob copy operation failure.
   */
  public trackCopyOperationFailed(
    operation: BlobCopyOperation,
    errorMessage: string,
    durationSeconds: number
  ): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyFailed',
        properties: {
          operationId: operation.Id,
          sourceUri: this.maskUri(operation.SourceUri),
          destinationUri: this.maskUri(operation.DestinationUri),
          errorMessage,
          errorCode: operation.ErrorCode || 'UNKNOWN',
        },
        measurements: {
          bytesCopied: operation.BytesCopied || 0,
          totalBytes: operation.TotalBytes,
          durationSeconds,
        },
      }
    );
  }

  /**
   * Track blob copy operation cancellation.
   */
  public trackCopyOperationCancelled(
    operation: BlobCopyOperation,
    durationSeconds: number
  ): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyCancelled',
        properties: {
          operationId: operation.Id,
          sourceUri: this.maskUri(operation.SourceUri),
          destinationUri: this.maskUri(operation.DestinationUri),
        },
        measurements: {
          bytesCopied: operation.BytesCopied || 0,
          totalBytes: operation.TotalBytes,
          durationSeconds,
        },
      }
    );
  }

  /**
   * Track validation errors.
   */
  public trackValidationError(errorMessage: string, errorCode?: string): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'BlobCopyValidationError',
        properties: {
          errorMessage,
          errorCode: errorCode || 'UNKNOWN',
        },
      }
    );
  }

  /**
   * Track API request latency.
   */
  public trackApiLatency(
    endpoint: string,
    durationMs: number,
    statusCode: number
  ): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'ApiLatency',
        properties: {
          endpoint,
          statusCode: statusCode.toString(),
        },
        measurements: {
          durationMs,
        },
      }
    );
  }

  /**
   * Track exception/error.
   */
  public trackException(error: Error | string): void {
    if (!this.appInsights) return;

    if (error instanceof Error) {
      this.appInsights.trackException({ error });
    } else {
      this.appInsights.trackTrace({
        message: String(error),
        severityLevel: 2, // Error level
      });
    }
  }

  /**
   * Track custom metric.
   */
  public trackMetric(name: string, value: number): void {
    if (!this.appInsights) return;

    this.appInsights.trackEvent(
      {
        name: 'CustomMetric',
        properties: {
          metricName: name,
        },
        measurements: {
          value,
        },
      }
    );
  }

  /**
   * Mask sensitive parts of URI for logging.
   */
  private maskUri(uri: string): string {
    try {
      const url = new URL(uri);
      const parts = url.pathname.split('/').filter(p => p);
      return `${url.protocol}//${url.hostname}/${parts.slice(0, 2).join('/')}/*`;
    } catch {
      return '***';
    }
  }

  /**
   * Calculate throughput in MB/s.
   */
  private calculateThroughput(bytes: number, seconds: number): number {
    if (seconds === 0) return 0;
    const megabytes = bytes / (1024 * 1024);
    return megabytes / seconds;
  }

  /**
   * Flush pending telemetry to Application Insights.
   */
  public flush(): void {
    if (this.appInsights) {
      this.appInsights.flush();
    }
  }

  /**
   * Check if Application Insights is initialized.
   */
  public isInitialized(): boolean {
    return this.appInsights !== null && this.instrumentationKey !== null;
  }
}

// Export singleton instance
export const appInsightsService = new ApplicationInsightsService();
