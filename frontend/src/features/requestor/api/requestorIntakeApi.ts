import { apiRequest } from "../../../shared/utils/apiClient";

export interface RequestorIntakeDto {
  connectedNodes: number;
  tasksPerHour: number;
  totalProvidedTasks: number;
  availableTasks: number;
  totalEarnings: string;
  taskThroughput: string;
}

export const getRequestorIntake = () =>
  apiRequest<RequestorIntakeDto>("/api/tasks/requestor-intake");