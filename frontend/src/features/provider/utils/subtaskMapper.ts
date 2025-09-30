import type { ProviderSubtaskDto, ProviderTaskType, ResourceSpecification, SubtaskStatus } from '../types';

const TASK_TYPE_MAP: Record<number, ProviderTaskType> = {
  0: 'Train',
  1: 'Inference'
};

const SUBTASK_STATUS_MAP: Record<number, SubtaskStatus> = {
  0: 'Pending',
  1: 'Assigned',
  2: 'Executing',
  3: 'Completed',
  4: 'Failed'
};

const toNumber = (value: unknown): number | undefined => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? undefined : parsed;
  }
  return undefined;
};

const toStringValue = (value: unknown): string | undefined => {
  if (typeof value === 'string') {
    return value;
  }
  return undefined;
};

const resolveTaskType = (raw: unknown): ProviderTaskType => {
  if (typeof raw === 'string') {
    return (raw as ProviderTaskType) || 'Inference';
  }
  if (typeof raw === 'number') {
    return TASK_TYPE_MAP[raw] ?? 'Inference';
  }
  return 'Inference';
};

const resolveStatus = (raw: unknown): SubtaskStatus => {
  if (typeof raw === 'string') {
    return (raw as SubtaskStatus) || 'Pending';
  }
  if (typeof raw === 'number') {
    return SUBTASK_STATUS_MAP[raw] ?? 'Pending';
  }
  return 'Pending';
};

const mapResourceSpecification = (input: unknown): ResourceSpecification => {
  const record = (input as Record<string, unknown>) ?? {};
  const gpuUnits = toNumber(record.gpuUnits ?? record.GpuUnits) ?? 0;
  const cpuCores = toNumber(record.cpuCores ?? record.CpuCores) ?? 0;
  const diskGb = toNumber(record.diskGb ?? record.DiskGb) ?? 0;
  const networkGb = toNumber(record.networkGb ?? record.NetworkGb) ?? 0;
  const dataSizeGb = toNumber(record.dataSizeGb ?? record.DataSizeGb) ?? 0;

  return {
    gpuUnits,
    cpuCores,
    diskGb,
    networkGb,
    dataSizeGb
  };
};

export const mapHubSubtaskToProviderSubtask = (input: unknown): ProviderSubtaskDto | null => {
  if (!input || typeof input !== 'object') {
    return null;
  }

  const record = input as Record<string, unknown>;
  const id = (record.id ?? record.Id) as string | undefined;
  const taskId = (record.taskId ?? record.TaskId) as string | undefined;

  if (!id || !taskId) {
    return null;
  }

  const taskType = resolveTaskType(record.taskType ?? record.TaskType);
  const status = resolveStatus(record.status ?? record.Status);
  const progress = toNumber(record.progress ?? record.Progress) ?? 0;
  const parametersJson =
    toStringValue(record.parametersJson ?? record.ParametersJson) ?? '{}';
  const estimatedEarnings =
    toNumber(record.estimatedEarnings ?? record.EstimatedEarnings) ?? 0;
  const createdAtUtc =
    toStringValue(record.createdAtUtc ?? record.CreatedAtUtc) ?? new Date().toISOString();
  const resourceRequirements = mapResourceSpecification(
    record.resourceRequirements ?? record.ResourceRequirements
  );
  const durationSeconds =
    toNumber(record.durationSeconds ?? record.DurationSeconds) ?? null;

  return {
    id,
    taskId,
    taskType,
    status,
    progress,
    parametersJson,
    estimatedEarnings,
    resourceRequirements,
    createdAtUtc,
    durationSeconds
  };
};