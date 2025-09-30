import { useMutation, useQueryClient } from '@tanstack/react-query';
import { completeSubtask } from '../api';
import { invalidateAvailableSubtasksKey } from '../queries/useAvailableSubtasksQuery';
import { invalidateDeviceSubtasksKey } from '../queries/useDeviceSubtasksQuery';
import type { ProviderSubtaskExecutionResult } from '../types';

export const useCompleteSubtaskMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      subtaskId,
      results
    }: {
      subtaskId: string;
      results: ProviderSubtaskExecutionResult;
    }) => completeSubtask(subtaskId, results),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invalidateAvailableSubtasksKey });
      queryClient.invalidateQueries({ queryKey: invalidateDeviceSubtasksKey });
    }
  });
};