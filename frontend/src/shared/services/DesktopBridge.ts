type DesktopBridgeHandler<T = unknown> = (payload: T) => void;

interface DesktopBridgeApi {
  invoke<T = unknown>(methodName: string, payload?: unknown): Promise<T>;
  emit(eventName: string, payload?: unknown): void;
  on<T = unknown>(eventName: string, handler: DesktopBridgeHandler<T>): void;
  off<T = unknown>(eventName: string, handler: DesktopBridgeHandler<T>): void;
  getDeviceIdentifier(): Promise<string | null>;
}

export interface HardwareMetrics {
  timestamp: string | null;
  cpuCores: number | null;
  cpuFrequencyGhz: number | null;
  videoMemoryAvailable: number | null;
  gpuName: string | null;
  gpuVendor: string | null;
  memoryTotalGb: number | null;
  memoryAvailableGb: number | null;
  networkDownlinkMbps: number | null;
  networkLatencyMs: number | null;
  storageFreeGb: number | null;
  storageTotalGb: number | null;
}

interface HardwareMetricsRaw extends Partial<Record<keyof HardwareMetrics, unknown>> {
  timestamp?: string | Date;
}

declare global {
  interface Window {
    DesktopBridge?: DesktopBridgeApi;
  }
}

const getBridge = (): DesktopBridgeApi | null => {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.DesktopBridge ?? null;
};

const coerceNumber = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
};

const normalizeHardwareMetrics = (raw: HardwareMetricsRaw): HardwareMetrics => {
  const isoTimestamp = (() => {
    if (!raw.timestamp) {
      return null;
    }

    if (raw.timestamp instanceof Date && !Number.isNaN(raw.timestamp.getTime())) {
      return raw.timestamp.toISOString();
    }

    if (typeof raw.timestamp === 'string') {
      const parsed = new Date(raw.timestamp);
      if (!Number.isNaN(parsed.getTime())) {
        return parsed.toISOString();
      }
    }

    return null;
  })();

  return {
    timestamp: isoTimestamp,
    cpuCores: coerceNumber(raw.cpuCores),
    cpuFrequencyGhz: coerceNumber(raw.cpuFrequencyGhz),
    videoMemoryAvailable: coerceNumber(raw.videoMemoryAvailable),
    gpuName: (typeof raw.gpuName === 'string' && raw.gpuName.length > 0) ? raw.gpuName : null,
    gpuVendor: (typeof raw.gpuVendor === 'string' && raw.gpuVendor.length > 0) ? raw.gpuVendor : null,
    memoryTotalGb: coerceNumber(raw.memoryTotalGb),
    memoryAvailableGb: coerceNumber(raw.memoryAvailableGb),
    networkDownlinkMbps: coerceNumber(raw.networkDownlinkMbps),
    networkLatencyMs: coerceNumber(raw.networkLatencyMs),
    storageFreeGb: coerceNumber(raw.storageFreeGb),
    storageTotalGb: coerceNumber(raw.storageTotalGb)
  };
};

export const DesktopBridge = {
  isAvailable(): boolean {
    return getBridge() !== null;
  },

  async invoke<T = unknown>(methodName: string, payload?: unknown): Promise<T> {
    const bridge = getBridge();
    if (!bridge) {
      throw new Error(`Desktop bridge is not available. Unable to invoke "${methodName}".`);
    }

    return bridge.invoke<T>(methodName, payload);
  },

  emit(eventName: string, payload?: unknown): void {
    const bridge = getBridge();
    if (!bridge) {
      return;
    }

    bridge.emit(eventName, payload);
  },

  on<T = unknown>(eventName: string, handler: DesktopBridgeHandler<T>): () => void {
    const bridge = getBridge();
    if (!bridge) {
      return () => {};
    }

    const wrapped: DesktopBridgeHandler = (payload) => {
      handler(payload as T);
    };

    bridge.on(eventName, wrapped);
    return () => {
      bridge.off(eventName, wrapped);
    };
  },

  off<T = unknown>(eventName: string, handler: DesktopBridgeHandler<T>): void {
    const bridge = getBridge();
    if (!bridge) {
      return;
    }

    bridge.off(eventName, handler);
  },

  async getHardwareMetrics(): Promise<HardwareMetrics> {
    const raw = await DesktopBridge.invoke<HardwareMetricsRaw>('hardware:getMetrics');
    return normalizeHardwareMetrics(raw ?? {});
  },

  async getDeviceIdentifier(): Promise<string | null> {
    try {
      const response = await DesktopBridge.invoke<{ identifier?: string | null }>('device:getIdentifier');
      const identifier = response?.identifier;
      return typeof identifier === 'string' && identifier.trim().length > 0 ? identifier.trim() : null;
    } catch {
      return null;
    }
  }
};

export type { DesktopBridgeHandler };