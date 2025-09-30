import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type { ProviderSubtaskDto, ProviderSubtaskExecutionResult, SubtaskStatus } from '../types';

type ExecutionPhase = 'idle' | 'initializing' | 'executing' | 'uploading' | 'completed' | 'failed';

interface ExecutionProgress {
  percentage: number;
  message?: string;
  lastUpdated?: string;
}

interface ActiveExecution {
  subtask: ProviderSubtaskDto;
  phase: ExecutionPhase;
  startedAt?: string;
  completedAt?: string;
  progress: ExecutionProgress;
  results?: ProviderSubtaskExecutionResult;
  errorMessage?: string;
}

interface ProviderExecutionState {
  active?: ActiveExecution;
  recentlyCompleted: ProviderSubtaskExecutionResult[];
  setActiveSubtask: (subtask: ProviderSubtaskDto) => void;
  syncActiveSubtask: (subtask: ProviderSubtaskDto) => void;
  setPhase: (phase: ExecutionPhase, message?: string) => void;
  setProgress: (progress: Partial<ExecutionProgress>) => void;
  setResults: (results: ProviderSubtaskExecutionResult) => void;
  failExecution: (message: string) => void;
  clearActive: () => void;
  clearHistory: () => void;
  updateActiveStatus: (status: SubtaskStatus, progress?: number) => void;
}

export const useProviderExecutionStore = create<ProviderExecutionState>()(
  devtools((set) => ({
    active: undefined,
    recentlyCompleted: [],
    setActiveSubtask: (subtask) =>
      set(() => {
        const timestamp = new Date().toISOString();
        return {
          active: {
            subtask,
            phase: 'initializing',
            startedAt: timestamp,
            progress: {
              percentage: subtask.progress ?? 0,
              message: 'Preparing browser execution environment',
              lastUpdated: timestamp
            }
          }
        };
      }),
    syncActiveSubtask: (subtask) =>
      set((state) => {
        if (!state.active || state.active.subtask.id !== subtask.id) {
          return state;
        }

        const timestamp = new Date().toISOString();
        return {
          active: {
            ...state.active,
            subtask,
            progress: {
              ...state.active.progress,
              percentage: subtask.progress ?? state.active.progress.percentage,
              lastUpdated: timestamp
            }
          }
        };
      }),
    setPhase: (phase, message) =>
      set((state) => {
        if (!state.active) {
          return state;
        }

        return {
          active: {
            ...state.active,
            phase,
            progress: {
              ...state.active.progress,
              message: message ?? state.active.progress.message,
              lastUpdated: new Date().toISOString()
            }
          }
        };
      }),
    setProgress: (progress) =>
      set((state) => {
        if (!state.active) {
          return state;
        }

        const nextProgress: ExecutionProgress = {
          percentage: progress.percentage ?? state.active.progress.percentage,
          message: progress.message ?? state.active.progress.message,
          lastUpdated: progress.lastUpdated ?? new Date().toISOString()
        };

        // eslint-disable-next-line no-console
        console.debug('[ProviderExecutionStore] setProgress', {
          subtaskId: state.active.subtask.id,
          patch: progress,
          previous: state.active.progress,
          next: nextProgress
        });

        return {
          active: {
            ...state.active,
            progress: nextProgress
          }
        };
      }),
    setResults: (results) =>
      set((state) => {
        if (!state.active) {
          return state;
        }

        const completedExecution: ActiveExecution = {
          ...state.active,
          phase: 'completed',
          completedAt: new Date().toISOString(),
          results,
          progress: {
            percentage: 100,
            message: 'Execution completed',
            lastUpdated: new Date().toISOString()
          }
        };

        return {
          active: completedExecution,
          recentlyCompleted: [results, ...state.recentlyCompleted].slice(0, 10)
        };
      }),
    failExecution: (message) =>
      set((state) => {
        if (!state.active) {
          return state;
        }

        return {
          active: {
            ...state.active,
            phase: 'failed',
            errorMessage: message,
            progress: {
              ...state.active.progress,
              message,
              lastUpdated: new Date().toISOString()
            }
          }
        };
      }),
    clearActive: () =>
      set({
        active: undefined
      }),
    clearHistory: () =>
      set({
        recentlyCompleted: []
      }),
    updateActiveStatus: (status, progress) =>
      set((state) => {
        if (!state.active || state.active.subtask.id === undefined) {
          return state;
        }

        const nextSubtask = {
          ...state.active.subtask,
          status,
          progress: progress ?? state.active.subtask.progress
        };

        const nextProgress: ExecutionProgress = {
          ...state.active.progress,
          percentage: progress ?? state.active.progress.percentage,
          lastUpdated: new Date().toISOString()
        };

        const nextActive: ActiveExecution = {
          ...state.active,
          subtask: nextSubtask,
          progress: nextProgress
        };

        // eslint-disable-next-line no-console
        console.debug('[ProviderExecutionStore] updateActiveStatus', {
          subtaskId: state.active.subtask.id,
          status,
          progressOverride: progress,
          nextProgress
        });

        return {
          active: nextActive
        };
      })
  }))
);