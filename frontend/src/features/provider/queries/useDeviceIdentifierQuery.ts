import { useQuery } from '@tanstack/react-query';
import { DesktopBridge } from '../../../shared/services/DesktopBridge';

const QUERY_KEY = ['provider', 'device', 'identifier'] as const;

const fetchDeviceIdentifier = async (): Promise<string | null> => {
  if (!DesktopBridge.isAvailable()) {
    return null;
  }

  try {
    const identifier = await DesktopBridge.getDeviceIdentifier();

    if (typeof identifier === 'string' && identifier.trim().length > 0) {
      return identifier.trim();
    }

    return null;
  } catch {
    return null;
  }
};

export const useDeviceIdentifierQuery = () =>
  useQuery({
    queryKey: QUERY_KEY,
    queryFn: fetchDeviceIdentifier,
    staleTime: Infinity,
    gcTime: Infinity,
    retry: 1
  });

export const deviceIdentifierQueryKey = QUERY_KEY;