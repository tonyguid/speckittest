import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { ProgressUpdate, BlobCopyOperation } from '../types';

/**
 * Callbacks for SignalR events.
 */
export interface SignalRCallbacks {
  onProgressUpdate?: (progress: ProgressUpdate) => void;
  onCopyCompleted?: (operation: BlobCopyOperation) => void;
  onCopyFailed?: (operation: BlobCopyOperation) => void;
  onOperationCancelled?: (operation: BlobCopyOperation) => void;
  onConnectionEstablished?: () => void;
  onConnectionError?: (error: Error) => void;
  onConnectionClosed?: () => void;
}

/**
 * SignalR client for real-time progress updates.
 * Manages WebSocket connection and handles server-pushed notifications.
 */
export class SignalRClient {
  private connection: HubConnection | null = null;
  private hubURL: string;
  private callbacks: SignalRCallbacks = {};
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 3000;

  constructor(hubURL: string = '/blobcopyhub') {
    this.hubURL = hubURL;
  }

  /**
   * Connects to the SignalR hub with optional subscription to an operation.
   *
   * @param operationId - Optional operation ID to subscribe to updates
   * @param callbacks - Event callbacks
   * @returns Promise that resolves when connected
   * @throws Error if connection fails
   */
  async connect(
    operationId?: string,
    callbacks?: SignalRCallbacks
  ): Promise<void> {
    if (this.connection?.state === 'Connected') {
      if (operationId) {
        await this.subscribeToOperation(operationId);
      }
      return;
    }

    if (callbacks) {
      this.callbacks = callbacks;
    }

    try {
      this.connection = new HubConnectionBuilder()
        .withUrl(this.hubURL, {
          withCredentials: true,
          automaticReconnect: true,
        })
        .withAutomaticReconnect([
          0,
          3000,
          5000,
          10000,
          15000,
          30000,
        ])
        .configureLogging(LogLevel.Information)
        .build();

      // Register event handlers
      this.setupEventHandlers();

      // Connect
      await this.connection.start();
      this.reconnectAttempts = 0;

      // Notify callback
      this.callbacks.onConnectionEstablished?.();

      // Subscribe to operation if provided
      if (operationId) {
        await this.subscribeToOperation(operationId);
      }
    } catch (error) {
      this.handleConnectionError(error);
      throw error;
    }
  }

  /**
   * Disconnects from the SignalR hub.
   *
   * @returns Promise that resolves when disconnected
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        console.error('Error disconnecting from SignalR:', error);
      }
    }
    this.connection = null;
  }

  /**
   * Subscribes to progress updates for a specific operation.
   *
   * @param operationId - Operation ID to subscribe to
   * @returns Promise that resolves when subscribed
   */
  async subscribeToOperation(operationId: string): Promise<void> {
    if (!this.connection || this.connection.state !== 'Connected') {
      throw new Error('SignalR connection not established');
    }

    try {
      await this.connection.invoke('JoinOperationGroup', operationId);
    } catch (error) {
      console.error(`Failed to subscribe to operation ${operationId}:`, error);
      throw error;
    }
  }

  /**
   * Unsubscribes from progress updates for a specific operation.
   *
   * @param operationId - Operation ID to unsubscribe from
   * @returns Promise that resolves when unsubscribed
   */
  async unsubscribeFromOperation(operationId: string): Promise<void> {
    if (!this.connection || this.connection.state !== 'Connected') {
      throw new Error('SignalR connection not established');
    }

    try {
      await this.connection.invoke('LeaveOperationGroup', operationId);
    } catch (error) {
      console.error(
        `Failed to unsubscribe from operation ${operationId}:`,
        error
      );
      throw error;
    }
  }

  /**
   * Checks if currently connected to the hub.
   *
   * @returns True if connected
   */
  isConnected(): boolean {
    return this.connection?.state === 'Connected';
  }

  /**
   * Sets up SignalR event handlers for server-pushed messages.
   */
  private setupEventHandlers(): void {
    if (!this.connection) return;

    // Progress update
    this.connection.on('ProgressUpdate', (progress: ProgressUpdate) => {
      this.callbacks.onProgressUpdate?.(progress);
    });

    // Copy completed
    this.connection.on('CopyCompleted', (operation: BlobCopyOperation) => {
      this.callbacks.onCopyCompleted?.(operation);
    });

    // Copy failed
    this.connection.on('CopyFailed', (operation: BlobCopyOperation) => {
      this.callbacks.onCopyFailed?.(operation);
    });

    // Operation cancelled
    this.connection.on('OperationCancelled', (operation: BlobCopyOperation) => {
      this.callbacks.onOperationCancelled?.(operation);
    });

    // Connection established
    this.connection.on('ConnectionEstablished', () => {
      this.callbacks.onConnectionEstablished?.();
    });

    // Reconnected
    this.connection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.callbacks.onConnectionEstablished?.();
    });

    // Disconnected
    this.connection.onclose(() => {
      this.callbacks.onConnectionClosed?.();
    });
  }

  /**
   * Handles connection errors with optional retry logic.
   *
   * @param error - Connection error
   */
  private handleConnectionError(error: any): void {
    const errorObj = error instanceof Error ? error : new Error(String(error));
    this.callbacks.onConnectionError?.(errorObj);
    console.error('SignalR connection error:', errorObj);
  }

  /**
   * Sets the hub URL (useful for testing or environment changes).
   *
   * @param hubURL - New hub URL
   */
  setHubURL(hubURL: string): void {
    if (this.connection?.state === 'Connected') {
      throw new Error('Cannot change hub URL while connected');
    }
    this.hubURL = hubURL;
  }

  /**
   * Registers a callback without reconnecting.
   *
   * @param callbacks - Event callbacks to register
   */
  registerCallbacks(callbacks: Partial<SignalRCallbacks>): void {
    this.callbacks = { ...this.callbacks, ...callbacks };

    // If already connected, re-setup handlers
    if (this.connection?.state === 'Connected') {
      this.setupEventHandlers();
    }
  }
}

// Export singleton instance
export const signalRClient = new SignalRClient();
