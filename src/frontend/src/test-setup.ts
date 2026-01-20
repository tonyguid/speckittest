import '@testing-library/jest-dom';

// Mock SignalR
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => ({
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    build: vi.fn().mockReturnValue({
      start: vi.fn().mockResolvedValue(undefined),
      stop: vi.fn().mockResolvedValue(undefined),
      on: vi.fn(),
      off: vi.fn(),
      invoke: vi.fn(),
      state: 'Connected',
    }),
  })),
  HubConnectionState: {
    Disconnected: 'Disconnected',
    Connecting: 'Connecting',
    Connected: 'Connected',
    Disconnecting: 'Disconnecting',
    Reconnecting: 'Reconnecting',
  },
}));

// Mock Application Insights
vi.mock('./services/applicationInsightsService', () => ({
  appInsightsService: {
    trackCopyOperationStarted: vi.fn(),
    trackCopyOperationCompleted: vi.fn(),
    trackCopyOperationFailed: vi.fn(),
    trackCopyOperationCancelled: vi.fn(),
    trackProgressUpdate: vi.fn(),
    trackException: vi.fn(),
  },
}));
