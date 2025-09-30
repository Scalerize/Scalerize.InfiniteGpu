export const RequestorTaskType = {
  Train: 0,
  Inference: 1
} as const;
export type RequestorTaskType = (typeof RequestorTaskType)[keyof typeof RequestorTaskType];

export const RequestorTaskStatus = {
  Pending: 'Pending',
  Assigned: 'Assigned',
  InProgress: 'InProgress',
  Completed: 'Completed',
  Failed: 'Failed'
} as const;
export type RequestorTaskStatus = (typeof RequestorTaskStatus)[keyof typeof RequestorTaskStatus];

export const RequestorPartitionCompilationStatus = {
  Pending: 0,
  InProgress: 1,
  Completed: 2,
  Failed: 3
} as const;
export type RequestorPartitionCompilationStatus = (typeof RequestorPartitionCompilationStatus)[keyof typeof RequestorPartitionCompilationStatus];

export const RequestorInferencePayloadType = {
  Json: 'Json',
  Text: 'Text',
  Binary: 'Binary'
} as const;
export type RequestorInferencePayloadType =
  (typeof RequestorInferencePayloadType)[keyof typeof RequestorInferencePayloadType];

export interface RequestorTaskResourceSpecification {
  gpuUnits: number;
  cpuCores: number;
  diskGb: number;
  networkGb: number;
}

export interface RequestorTaskInferenceBindingDto {
  tensorName: string;
  payloadType: RequestorInferencePayloadType;
  payload?: string | null;
  fileUrl?: string | null;
}

export interface RequestorTaskInferenceDto {
  prompt: string;
  bindings: Array<RequestorTaskInferenceBindingDto>;
}

export interface RequestorTaskDto {
  id: string;
  type: RequestorTaskType;
  modelUrl: string;
  train?: {
    epochs: number;
    batchSize: number;
  } | null;
  inference?: RequestorTaskInferenceDto | null;
  resources: RequestorTaskResourceSpecification;
  dataSizeGb: number;
  status: RequestorTaskStatus;
  partitionStatus: RequestorPartitionCompilationStatus;
  estimatedCost: number;
  createdAt: string;
  updatedAt?: string | null;
  completedAt?: string | null;
  lastProgressAtUtc?: string | null;
  lastHeartbeatAtUtc?: string | null;
  subtasksCount: number;
  completedSubtasksCount: number;
  failedSubtasksCount: number;
  activeSubtasksCount: number;
  completionPercent: number;
  durationSeconds?: number | null;
  costUsd?: number | null;
}

export interface RequestorTask {
  id: string;
  label: string;
  artifactName: string;
  artifactUrl: string;
  type: RequestorTaskType;
  status: RequestorTaskStatus;
  partitionStatus: RequestorPartitionCompilationStatus;
  createdAt: string;
  updatedAt?: string | null;
  completedAt?: string | null;
  durationLabel: string;
  subtasksCount: number;
  cost: number;
  inferenceBindings: Array<RequestorTaskInferenceBindingDto>;
  completionPercent: number;
}

export const SubtaskStatus = {
  Pending: 'Pending',
  Assigned: 'Assigned',
  Executing: 'Executing',
  Completed: 'Completed',
  Failed: 'Failed'
} as const;
export type SubtaskStatus = (typeof SubtaskStatus)[keyof typeof SubtaskStatus];

export interface SubtaskTimelineEventDto {
  id: string;
  eventType: string;
  message?: string | null;
  metadataJson?: string | null;
  createdAtUtc: string;
}

export interface InputArtifactDto {
  tensorName: string;
  payloadType: string;
  fileUrl?: string | null;
  payload?: string | null;
}

export interface OutputArtifactDto {
  tensorName: string;
  payloadType: string;
  fileUrl?: string | null;
  fileFormat?: string | null;
  payload?: string | null;
}

export interface SubtaskDto {
  id: string;
  taskId: string;
  status: SubtaskStatus;
  createdAtUtc: string;
  assignedAtUtc?: string | null;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
  failedAtUtc?: string | null;
  failureReason?: string | null;
  durationSeconds?: number | null;
  costUsd?: number | null;
  executionArtifactsUrl?: string | null;
  outputArtifactBundleUri?: string | null;
  timeline: Array<SubtaskTimelineEventDto>;
  inputArtifacts: Array<InputArtifactDto>;
  outputArtifacts: Array<OutputArtifactDto>;
}