import { InfinityIcon, ClipboardList, Send, Gauge, Wallet } from 'lucide-react';
import { StatCard } from '../../provider/components/StatCard';
import { useRequestorIntakeQuery } from '../queries/useRequestorIntakeQuery';

export const RequestorIntakeCard = () => {
  const { data: intake, isLoading, isError } = useRequestorIntakeQuery();

  if (isLoading) {
    return (
      <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-100 dark:bg-slate-900 dark:ring-slate-800">
        <div className="bg-gradient-to-br from-slate-900 via-indigo-900 to-indigo-700 px-6 py-5 text-indigo-50 dark:from-indigo-950 dark:via-slate-950 dark:to-slate-900">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <span className="text-xs font-semibold uppercase tracking-wide text-indigo-200">
                Global network
              </span>
              <p className="mt-2 text-xl font-semibold text-white">Requestor intake</p>
              <p className="text-sm text-indigo-100">
                Loading metrics...
              </p>
            </div>
          </div>
        </div>
        <div className="space-y-6 p-6">
          <div className="animate-pulse">
            <div className="h-4 bg-slate-200 rounded w-1/4 mb-2 dark:bg-slate-700"></div>
            <div className="h-6 bg-slate-200 rounded w-1/2 mb-4 dark:bg-slate-700"></div>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-2">
              {[1, 2, 3, 4, 5].map((i) => (
                <div key={i} className="h-20 bg-slate-100 rounded-lg dark:bg-slate-800"></div>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (isError || !intake) {
    return (
      <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-100 dark:bg-slate-900 dark:ring-slate-800">
        <div className="bg-gradient-to-br from-slate-900 via-indigo-900 to-indigo-700 px-6 py-5 text-indigo-50 dark:from-indigo-950 dark:via-slate-950 dark:to-slate-900">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <span className="text-xs font-semibold uppercase tracking-wide text-indigo-200">
                Global network
              </span>
              <p className="mt-2 text-xl font-semibold text-white">Requestor intake</p>
              <p className="text-sm text-red-200">
                Unable to load metrics
              </p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-100 dark:bg-slate-900 dark:ring-slate-800">
      <div className="bg-gradient-to-br from-slate-900 via-indigo-900 to-indigo-700 px-6 py-5 text-indigo-50 dark:from-indigo-950 dark:via-slate-950 dark:to-slate-900">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-indigo-200">
              Global network
            </span>
            <p className="mt-2 text-xl font-semibold text-white">Requestor intake</p>
            <p className="text-sm text-indigo-100">
              Aggregated metrics across providers and requestors.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-lg bg-white/10 px-4 py-3 text-sm dark:bg-white/5">
              <span className="text-xs uppercase tracking-wide text-indigo-200 dark:text-indigo-300">Nodes</span>
              <p className="text-lg font-semibold text-white">
                {intake.connectedNodes.toLocaleString()}
              </p>
            </div>
            <div className="rounded-lg bg-white/10 px-4 py-3 text-sm dark:bg-white/5">
              <span className="text-nowrap text-xs uppercase tracking-wide text-indigo-200 dark:text-indigo-300">Task</span>
              <p className="text-nowrap text-lg font-semibold text-white">
                {intake.tasksPerHour} / h
              </p>
            </div>
          </div>
        </div>
      </div>
      <div className="space-y-6 p-6">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-indigo-500 dark:text-indigo-400">
              Network health
            </span>
            <h4 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Global workload distribution</h4>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Capacity, backlog, and settlement insights across the marketplace edge.
            </p>
          </div>
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-2">
          <StatCard
            icon={<InfinityIcon className="h-5 w-5" />}
            title="Connected nodes"
            value={intake.connectedNodes.toLocaleString()}
            description="Global mesh capacity online"
          />
          <StatCard
            icon={<ClipboardList className="h-5 w-5" />}
            title="Provided tasks"
            value={intake.totalProvidedTasks.toLocaleString()}
            description="Completed workloads over 24h"
          />
          <StatCard
            icon={<Send className="h-5 w-5" />}
            title="Available tasks"
            value={intake.availableTasks.toLocaleString()}
            description="Ready for assignment"
          />
          <StatCard
            icon={<Gauge className="h-5 w-5" />}
            title="Task throughput"
            value={intake.taskThroughput}
            description="Sustained execution velocity"
          />
          <StatCard
            icon={<Wallet className="h-5 w-5" />}
            title="Earnings (24h)"
            value={intake.totalEarnings}
            description="Settled across all providers"
          />
        </div>
      </div>
    </div>
  );
};