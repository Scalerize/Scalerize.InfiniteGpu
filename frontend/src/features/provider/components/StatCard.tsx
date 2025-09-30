import type { JSX } from 'react';

export type StatCardProps = {
  icon: JSX.Element;
  title: string;
  value: string;
  description: string;
};

export const StatCard = ({ icon, title, value, description }: StatCardProps) => (
  <div className="flex items-start gap-3 rounded-lg border border-slate-100 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800">
    <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-500/10 text-indigo-600 dark:bg-indigo-950/50 dark:text-indigo-400">
      {icon}
    </div>
    <div className="space-y-1">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">{title}</p>
      <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">{value}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{description}</p>
    </div>
  </div>
);