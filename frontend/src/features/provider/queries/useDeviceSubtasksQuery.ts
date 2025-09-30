import { useQuery } from '@tanstack/react-query';
import { fetchDeviceSubtasks } from '../api'; 

const QUERY_KEY = ['provider', 'subtasks', 'device'] as const;

export const useDeviceSubtasksQuery = (deviceIdentifier: string | null | undefined) =>
  useQuery({
    queryKey: [...QUERY_KEY, deviceIdentifier ?? null],
    queryFn: () => fetchDeviceSubtasks(deviceIdentifier!),
    enabled: typeof deviceIdentifier === 'string' && deviceIdentifier.length > 0,
    refetchInterval: 60000, // Poll every 1 minute
    staleTime: 1000 * 30
  });

export const invalidateDeviceSubtasksKey = QUERY_KEY;