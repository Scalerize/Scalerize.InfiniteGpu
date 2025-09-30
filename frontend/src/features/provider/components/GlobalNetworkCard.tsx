import { InfinityIcon, ClipboardList, Send, Gauge, Wallet } from 'lucide-react';
import { StatCard } from './StatCard';

export const GlobalNetworkCard = () => (
  <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-100">
    <div className="bg-gradient-to-br from-slate-900 via-indigo-900 to-indigo-700 px-6 py-5 text-indigo-50">
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
          <div className="rounded-lg bg-white/10 px-4 py-3 text-sm">
            <span className="text-xs uppercase tracking-wide text-indigo-200">Nodes</span>
            <p className="text-lg font-semibold text-white">1,284</p>
          </div>
          <div className="rounded-lg bg-white/10 px-4 py-3 text-sm">
            <span className="text-nowrap text-xs uppercase tracking-wide text-indigo-200">Task</span>
            <p className="text-nowrap text-lg font-semibold text-white">842 / h</p>
          </div>
        </div>
      </div>
    </div>
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <span className="text-xs font-semibold uppercase tracking-wide text-indigo-500">
            Network health
          </span>
          <h4 className="text-lg font-semibold text-slate-900">Global workload distribution</h4>
          <p className="text-sm text-slate-500">
            Capacity, backlog, and settlement insights across the marketplace edge.
          </p>
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-2">
        <StatCard
          icon={<InfinityIcon className="h-5 w-5" />}
          title="Connected nodes"
          value="1,284"
          description="Global mesh capacity online"
        />
        <StatCard
          icon={<ClipboardList className="h-5 w-5" />}
          title="Provided tasks"
          value="9,432"
          description="Completed workloads over 24h"
        />
        <StatCard
          icon={<Send className="h-5 w-5" />}
          title="Available tasks"
          value="317"
          description="Ready for partition assignment"
        />
        <StatCard
          icon={<Gauge className="h-5 w-5" />}
          title="Task throughput"
          value="842 tasks / hr"
          description="Sustained execution velocity"
        />
        <StatCard
          icon={<Wallet className="h-5 w-5" />}
          title="Earnings (24h)"
          value="$68.4k"
          description="Settled across all providers"
        />
      </div>
    </div>
  </div>
);