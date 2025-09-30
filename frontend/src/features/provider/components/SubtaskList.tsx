import { useMemo } from "react";
import { motion } from "framer-motion";
import { Clock, Gauge, Rocket, Timer, Wallet } from "lucide-react";
import { useAvailableSubtasksQuery } from "../queries/useAvailableSubtasksQuery";
import { useDeviceIdentifierQuery } from "../queries/useDeviceIdentifierQuery";
import { useDeviceSubtasksQuery } from "../queries/useDeviceSubtasksQuery";
import type { SubtaskStatus } from "../types";
import { getRelativeTime } from "../../../shared/utils/dateTime";

const SUBTASK_STATE_DISPLAY: Record<
  SubtaskStatus,
  {
    label: "In Progress" | "Failed" | "Succeeded";
    badgeClass: string;
    indicatorClass: string;
  }
> = {
  Pending: {
    label: "In Progress",
    badgeClass: "border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-900 dark:bg-indigo-950/50 dark:text-indigo-400",
    indicatorClass: "bg-indigo-500 dark:bg-indigo-400",
  },
  Assigned: {
    label: "In Progress",
    badgeClass: "border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-900 dark:bg-indigo-950/50 dark:text-indigo-400",
    indicatorClass: "bg-indigo-500 dark:bg-indigo-400",
  },
  Executing: {
    label: "In Progress",
    badgeClass: "border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-900 dark:bg-indigo-950/50 dark:text-indigo-400",
    indicatorClass: "bg-indigo-500 dark:bg-indigo-400",
  },
  Completed: {
    label: "Succeeded",
    badgeClass: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-400",
    indicatorClass: "bg-emerald-500 dark:bg-emerald-400",
  },
  Failed: {
    label: "Failed",
    badgeClass: "border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900 dark:bg-rose-950/50 dark:text-rose-400",
    indicatorClass: "bg-rose-500 dark:bg-rose-400",
  },
};

interface SubtaskListProps {
  executingSubtaskId?: string;
}

export const SubtaskList = ({ executingSubtaskId }: SubtaskListProps) => {
  const deviceIdentifierQuery = useDeviceIdentifierQuery();
  const deviceIdentifier = deviceIdentifierQuery.data ?? null;

  const deviceSubtasksQuery = useDeviceSubtasksQuery(deviceIdentifier);
  const availableSubtasksQuery = useAvailableSubtasksQuery(!deviceIdentifier);

  const useDeviceAssignments = !!deviceIdentifier;
  const activeQuery = useDeviceAssignments
    ? deviceSubtasksQuery
    : availableSubtasksQuery;

  const isLoading = deviceIdentifierQuery.isLoading || activeQuery.isLoading;
  const isError = activeQuery.isError;
  const data = activeQuery.data;

  const formatDuration = (seconds?: number | null) => {
    if (typeof seconds !== "number" || Number.isNaN(seconds) || seconds <= 0) {
      return "—";
    }

    const totalSeconds = Math.round(seconds);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const secs = totalSeconds % 60;

    const parts: string[] = [];
    if (hours > 0) {
      parts.push(`${hours}h`);
    }
    if (minutes > 0) {
      parts.push(`${minutes}m`);
    }
    if (hours === 0 && (minutes === 0 || secs > 0)) {
      parts.push(`${secs}s`);
    }

    return parts.join(" ");
  };

  const formatEarnings = (value?: number | null) => {
    if (typeof value !== "number" || Number.isNaN(value)) {
      return "—";
    }

    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "EUR",
      maximumFractionDigits: 2,
    }).format(value);
  };

  const sortedSubtasks = useMemo(() => {
    if (!data) {
      return [];
    }

    return [...data].sort(
      (a, b) =>
        new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
    );
  }, [data]);

  if (isLoading) {
    return (
      <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
        {useDeviceAssignments
          ? "Loading subtasks assigned to this device..."
          : "Loading subtasks for your capabilities..."}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/50 dark:text-red-400">
        Unable to load subtasks. Please retry shortly.
      </div>
    );
  }

  if (!sortedSubtasks.length) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-6 text-center dark:border-slate-700 dark:bg-slate-900">
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.35 }}
          className="flex flex-col items-center gap-2 text-sm text-slate-500 dark:text-slate-400"
        >
          <Rocket className="h-6 w-6 text-slate-400 dark:text-slate-500" />
          <span>
            {useDeviceAssignments
              ? "No subtasks are currently assigned to this device."
              : "No subtasks match your declared resources right now."}
          </span>
          <span>Keep this tab open to auto-refresh assignments.</span>
        </motion.div>
      </div>
    );
  }

  const getParametersJson = (subtask: any) => {
    const obj = JSON.parse(subtask.parametersJson || "{}");
    if (obj?.inference?.bindings?.[0]?.fileUrl) {
      obj.inference.bindings[0].fileUrl = "<redacted>";
    } else if (obj?.inference?.bindings?.[0]?.json) {
      obj.inference.bindings[0].json = "<redacted>";
    }
    return JSON.stringify(obj, null, 2);
  };

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      {sortedSubtasks.map((subtask) => {
        const isExecuting = executingSubtaskId === subtask.id;
        const displayState =
          SUBTASK_STATE_DISPLAY[subtask.status] ??
          SUBTASK_STATE_DISPLAY.Pending;

        return (
          <motion.div
            key={subtask.id}
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.25 }}
            className={`group h-full rounded-xl border bg-white shadow-sm transition hover:border-slate-300 dark:bg-slate-900 dark:hover:border-slate-600 ${
              isExecuting ? "border-emerald-400 shadow-md dark:border-emerald-600" : "border-slate-200 dark:border-slate-700"
            }`}
          >
            <div className="flex flex-col gap-3 p-5 md:flex-row md:items-start md:justify-between">
              <div className="flex flex-1 flex-col gap-3">
                <div className="flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-700 dark:text-slate-200">
                  <Gauge className="h-4 w-4 text-indigo-500 dark:text-indigo-400" />
                  <span>{subtask.taskType} Task</span>
                  <span
                    className={`inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-semibold ${displayState.badgeClass}`}
                  >
                    <span
                      className={`h-2.5 w-2.5 rounded-full ${displayState.indicatorClass}`}
                    />
                    <span>{displayState.label}</span>
                  </span>
                </div>
                <div className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                  <Clock className="h-3 w-3 text-slate-400 dark:text-slate-500" />
                  <span>Queued {getRelativeTime(subtask.createdAtUtc)}</span>
                </div>
                <details className="rounded-md border border-slate-200 bg-slate-50 text-xs text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300">
                  <summary className="cursor-pointer select-none px-3 py-2 text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Parameters
                  </summary>
                  <pre className="max-h-48 overflow-y-auto px-3 pb-3 text-xs text-slate-600 dark:text-slate-300">
                    {getParametersJson(subtask)}
                  </pre>
                </details>
                <div className="grid gap-2 text-sm sm:grid-cols-2">
                  <InfoStat
                    icon={<Timer className="h-4 w-4" />}
                    label="Duration"
                    value={formatDuration(subtask.durationSeconds)}
                  />
                  <InfoStat
                    icon={<Wallet className="h-4 w-4" />}
                    label="Estimated earnings"
                    value={formatEarnings(subtask.estimatedEarnings)}
                  />
                </div>
              </div>
            </div>
          </motion.div>
        );
      })}
    </div>
  );
};

const InfoStat = ({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
}) => (
  <div className="flex items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300">
    <span className="text-slate-400 dark:text-slate-500">{icon}</span>
    <div className="flex flex-col">
      <span className="font-medium text-slate-500 dark:text-slate-400">{label}</span>
      <span className="text-slate-700 dark:text-slate-200">{value}</span>
    </div>
  </div>
);
