export type SubtaskStatus = 'Pending' | 'Assigned' | 'Executing' | 'Completed' | 'Failed';

export type ProviderTaskType = 'Train' | 'Inference';

export interface ResourceSpecification {
  gpuUnits: number;
  cpuCores: number;
  diskGb: number;
  networkGb: number;
  dataSizeGb: number;
}

export interface ProviderSubtaskDto {
  id: string;
  taskId: string;
  taskType: ProviderTaskType;
  status: SubtaskStatus;
  progress: number;
  parametersJson: string;
  estimatedEarnings: number;
  resourceRequirements: ResourceSpecification;
  createdAtUtc: string;
  durationSeconds?: number | null;
  costUsd?: number | null;
}

export interface ProviderSubtaskExecutionResult {
  subtaskId: string;
  completedAtUtc: string;
  outputSummary: string;
  artifacts: Record<string, unknown>;
}

export interface ProgressEventPayload {
  SubtaskId: string;
  AssignedProviderId: string;
  Progress: number;
  LastHeartbeatAtUtc?: string;
}

export interface SubtaskAcceptedEventPayload {
  SubtaskId: string;
  AssignedProviderId: string;
  Status: SubtaskStatus;
  AssignedAtUtc: string;
}

export interface SubtaskCompleteEventPayload {
  SubtaskId: string;
  AssignedProviderId: string;
  CompletedAtUtc: string;
}

export interface AvailableSubtasksChangedEventPayload {
  SubtaskId: string;
  Status: SubtaskStatus;
  AcceptedByProviderId?: string;
  TimestampUtc: string;
}