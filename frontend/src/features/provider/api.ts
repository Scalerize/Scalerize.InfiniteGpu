import { apiRequest } from '../../shared/utils/apiClient';
import type { ProviderSubtaskDto, ProviderSubtaskExecutionResult } from './types';

export const fetchAvailableSubtasks = () => apiRequest<ProviderSubtaskDto[]>('/api/subtasks/available');

export const fetchDeviceSubtasks = (deviceIdentifier: string) =>
  apiRequest<ProviderSubtaskDto[]>(`/api/subtasks/device?identifier=${encodeURIComponent(deviceIdentifier)}`);

export const acceptSubtask = (subtaskId: string) =>
  apiRequest<void, undefined>(`/api/subtasks/${subtaskId}/accept`, {
    method: 'POST'
  });

export const completeSubtask = (subtaskId: string, results: ProviderSubtaskExecutionResult) =>
  apiRequest<void, { ResultsJson: string }>(`/api/subtasks/${subtaskId}/complete`, {
    method: 'POST',
    body: {
      ResultsJson: JSON.stringify(results)
    }
  });