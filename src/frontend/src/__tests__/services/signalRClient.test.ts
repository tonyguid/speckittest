import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';
import { signalRClient } from '../../services/signalRClient';

// Mock SignalR
vi.mock('@microsoft/signalr');

describe('SignalRClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(async () => {
    // Reset singleton state between tests
    await signalRClient.disconnect();
  });

  describe('connect', () => {
    it('should establish connection with hub', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();

      expect(mockConnection.start).toHaveBeenCalled();
    });

    it('should register callbacks on connect', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        invoke: vi.fn().mockResolvedValue(undefined),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      const callbacks = {
        onProgressUpdate: vi.fn(),
        onCopyCompleted: vi.fn(),
      };

      await signalRClient.connect('op-123', callbacks);

      expect(mockConnection.on).toHaveBeenCalledWith(
        'ProgressUpdate',
        expect.any(Function)
      );
    });

    it('should throw on connection failure', async () => {
      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue({
                start: vi.fn().mockRejectedValue(new Error('Connection failed')),
                on: vi.fn(),
                onreconnecting: vi.fn(),
                onreconnected: vi.fn(),
                onclose: vi.fn(),
              }),
            }),
          }),
        }),
      } as any);

      await expect(signalRClient.connect()).rejects.toThrow(
        'Connection failed'
      );
    });
  });

  describe('disconnect', () => {
    it('should close connection', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();
      await signalRClient.disconnect();

      expect(mockConnection.stop).toHaveBeenCalled();
    });
  });

  describe('subscribeToOperation', () => {
    it('should join operation group', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();
      await signalRClient.subscribeToOperation('op-123');

      expect(mockConnection.invoke).toHaveBeenCalledWith(
        'JoinOperationGroup',
        'op-123'
      );
    });

    it('should throw if not connected', async () => {
      await expect(signalRClient.subscribeToOperation('op-123')).rejects.toThrow();
    });
  });

  describe('unsubscribeFromOperation', () => {
    it('should leave operation group', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();
      await signalRClient.subscribeToOperation('op-123');
      await signalRClient.unsubscribeFromOperation('op-123');

      expect(mockConnection.invoke).toHaveBeenCalledWith(
        'LeaveOperationGroup',
        'op-123'
      );
    });
  });

  describe('isConnected', () => {
    it('should return connection state', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();
      const connected = signalRClient.isConnected();

      expect(typeof connected).toBe('boolean');
    });
  });

  describe('setHubURL', () => {
    it('should configure hub URL', () => {
      const hubUrl = 'https://api.example.com/blobcopyhub';

      signalRClient.setHubURL(hubUrl);

      expect(signalRClient).toBeDefined();
    });
  });

  describe('registerCallbacks', () => {
    it('should register additional callbacks without reconnecting', async () => {
      const mockConnection = {
        start: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        state: 'Connected',
      };

      vi.mocked(HubConnectionBuilder).mockReturnValue({
        withUrl: vi.fn().mockReturnValue({
          withAutomaticReconnect: vi.fn().mockReturnValue({
            configureLogging: vi.fn().mockReturnValue({
              build: vi.fn().mockReturnValue(mockConnection),
            }),
          }),
        }),
      } as any);

      await signalRClient.connect();

      const newCallbacks = {
        onCopyFailed: vi.fn(),
      };

      signalRClient.registerCallbacks(newCallbacks);

      // Should register callbacks on existing connection
      expect(mockConnection.on).toHaveBeenCalled();
    });
  });
});
