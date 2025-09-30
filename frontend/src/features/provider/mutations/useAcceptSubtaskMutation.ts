import { useMutation, useQueryClient } from '@tanstack/react-query';
import { acceptSubtask } from '../api';
import { invalidateAvailableSubtasksKey } from '../queries/useAvailableSubtasksQuery';
import { invalidateDeviceSubtasksKey } from '../queries/useDeviceSubtasksQuery';

export const useAcceptSubtaskMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (subtaskId: string) => acceptSubtask(subtaskId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invalidateAvailableSubtasksKey });
      queryClient.invalidateQueries({ queryKey: invalidateDeviceSubtasksKey });
    }
  });
};