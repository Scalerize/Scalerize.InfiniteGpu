import { useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../../shared/utils/apiClient';
import type { ProviderSubtaskDto } from '../types';
import { useAuthStore } from '../../auth/stores/authStore';

const QUERY_KEY = ['provider', 'subtasks', 'available'];

export const useAvailableSubtasksQuery = (enabled = true) => {
  const user = useAuthStore((state) => state.user);

  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => apiRequest<ProviderSubtaskDto[]>('/api/subtasks/available'),
    enabled: enabled && !!user && user.role === 'Provider',
    refetchInterval: 60000, // Poll every 1 minute
    staleTime: 1000 * 30
  });
};

export const invalidateAvailableSubtasksKey = QUERY_KEY;