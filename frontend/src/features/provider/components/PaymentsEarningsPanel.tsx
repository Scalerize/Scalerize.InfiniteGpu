import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowDownLeft, ArrowUpRight, Banknote, CalendarDays, PiggyBank, Wallet, Plus } from 'lucide-react';
import { formatUtcToLocal } from '../../../shared/utils/dateTime';
import { getFinanceSummary, processTopUp, createSettlement } from '../api/financeApi';
import { TopUpDialog } from './TopUpDialog';
import { SettlementDialog } from './SettlementDialog';

export const PaymentsEarningsPanel = () => {
  const [isTopUpDialogOpen, setIsTopUpDialogOpen] = useState(false);
  const [isSettlementDialogOpen, setIsSettlementDialogOpen] = useState(false);
  const queryClient = useQueryClient();

  const { data: financeSummary, isLoading, error } = useQuery({
    queryKey: ['financeSummary'],
    queryFn: getFinanceSummary,
    refetchInterval: 30000, // Refetch every 30 seconds
  });

  const topUpMutation = useMutation({
    mutationFn: ({ amount, paymentMethodId }: { amount: number; paymentMethodId: string }) =>
      processTopUp(amount, paymentMethodId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['financeSummary'] });
    },
  });

  const settlementMutation = useMutation({
    mutationFn: ({ amount, country, bankAccountDetails }: { amount: number; country: string; bankAccountDetails: string }) =>
      createSettlement(amount, country, bankAccountDetails),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['financeSummary'] });
    },
  });

  const handleTopUp = async (amount: number, paymentMethodId: string) => {
    await topUpMutation.mutateAsync({ amount, paymentMethodId });
  };

  const handleSettle = async (amount: number, country: string, bankAccountDetails: string) => {
    await settlementMutation.mutateAsync({ amount, country, bankAccountDetails });
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-slate-500 dark:text-slate-400">Loading financial data...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-rose-600 dark:text-rose-400">Error loading financial data</div>
      </div>
    );
  }

  if (!financeSummary) {
    return null;
  }

  const ledgerEntries = financeSummary.ledgerEntries.map((entry) => ({
    id: entry.entryId,
    type: entry.kind === 'Credit' ? 'credit' as const : 'debit' as const,
    label: entry.title,
    detail: entry.detail || '',
    amount: entry.amount,
    createdAt: entry.occurredAtUtc,
    balanceAfter: entry.balanceAfter,
  }));
  return (
    <>
      <div className="space-y-8">
        <section className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
          <SummaryCard
            icon={<Wallet className="h-5 w-5 text-indigo-500" />}
            title="Available balance"
            value={`$${financeSummary.balance.toFixed(2)}`}
            helper="Current account balance"
            action={
              <button
                onClick={() => setIsTopUpDialogOpen(true)}
                className="mt-2 flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-700 transition dark:text-indigo-400 dark:hover:text-indigo-300"
              >
                <Plus className="h-3 w-3" />
                Top Up
              </button>
            }
          />
          <SummaryCard
            icon={<ArrowUpRight className="h-5 w-5 text-emerald-500" />}
            title="Credits (24h)"
            value={`$${financeSummary.creditsLast24Hours.toFixed(2)}`}
            helper="Earnings from completed tasks"
          />
          <SummaryCard
            icon={<ArrowDownLeft className="h-5 w-5 text-rose-500" />}
            title="Debits (24h)"
            value={`$${financeSummary.debitsLast24Hours.toFixed(2)}`}
            helper="Task execution costs"
          />
          <SummaryCard
            icon={<PiggyBank className="h-5 w-5 text-amber-500" />}
            title="Next payout"
            value={financeSummary.nextPayout ? `$${financeSummary.nextPayout.amount.toFixed(2)}` : '$0.00'}
            helper={financeSummary.nextPayout ? 'Processing settlement' : 'No pending payout'}
            action={
              <button
                onClick={() => setIsSettlementDialogOpen(true)}
                disabled={financeSummary.balance < 30}
                className="mt-2 flex items-center gap-1 text-xs font-medium text-emerald-600 hover:text-emerald-700 transition disabled:opacity-50 disabled:cursor-not-allowed dark:text-emerald-400 dark:hover:text-emerald-300"
              >
                <Plus className="h-3 w-3" />
                New Settlement
              </button>
            }
          />
        </section>

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm lg:col-span-2 dark:border-slate-700 dark:bg-slate-900">
          <header className="flex flex-col gap-1">
            <span className="text-sm font-semibold uppercase tracking-wide text-indigo-500 dark:text-indigo-400">Ledger</span>
            <div className="flex items-baseline justify-between gap-3">
              <h3 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Credits & debits</h3>
            </div>
          </header>

          <ul className="divide-y divide-slate-100 dark:divide-slate-700">
            {ledgerEntries.map((entry) => {
              const isCredit = entry.type === 'credit';
              return (
                <li key={entry.id} className="flex flex-wrap items-center gap-4 py-4">
                  <div
                    className={`flex h-11 w-11 items-center justify-center rounded-full ${
                      isCredit ? 'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/50 dark:text-emerald-400' : 'bg-rose-50 text-rose-600 dark:bg-rose-950/50 dark:text-rose-400'
                    }`}
                  >
                    {isCredit ? <ArrowDownLeft className="h-5 w-5" /> : <ArrowUpRight className="h-5 w-5" />}
                  </div>

                  <div className="flex min-w-0 flex-1 flex-col">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="truncate text-sm font-semibold text-slate-800 dark:text-slate-200">{entry.label}</span>
                      <span className="text-xs text-slate-400 dark:text-slate-500">
                        {formatUtcToLocal(entry.createdAt, {
                          hour: '2-digit',
                          minute: '2-digit',
                          day: '2-digit',
                          month: 'short'
                        })}
                      </span>
                    </div>
                    <p className="text-sm text-slate-500 dark:text-slate-400">{entry.detail}</p>
                    <div className="mt-2 flex flex-wrap items-center gap-4 text-xs text-slate-500 dark:text-slate-400">
                      <span className="rounded-full border border-slate-200 px-3 py-1 dark:border-slate-700">
                        Balance after: <span className="font-medium text-slate-700 dark:text-slate-300">€{entry.balanceAfter.toFixed(2)}</span>
                      </span>
                      <span className="rounded-full border border-slate-200 px-3 py-1 dark:border-slate-700">Entry ID: {entry.id}</span>
                    </div>
                  </div>

                  <div className="flex flex-col items-end">
                    <span
                      className={`text-sm font-semibold ${isCredit ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'}`}
                    >
                      {isCredit ? '+' : '-'}€{entry.amount.toFixed(2)}
                    </span>
                    <span className="text-xs text-slate-400 dark:text-slate-500">Auto-matched</span>
                  </div>
                </li>
              );
            })}
          </ul>
        </section>

        <section className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
          <header className="flex items-center justify-between">
            <div>
              <span className="text-sm font-semibold uppercase tracking-wide text-indigo-500 dark:text-indigo-400">Payouts</span>
              <h3 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Settlement timeline</h3>
            </div>
            <Banknote className="h-6 w-6 text-slate-300 dark:text-slate-600" />
          </header>

          <div className="space-y-4">
            {financeSummary.nextPayout && (
              <div className="rounded-lg border border-slate-100 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 text-sm font-semibold text-slate-800 dark:text-slate-200">
                      <CalendarDays className="h-4 w-4 text-indigo-500 dark:text-indigo-400" />
                      {financeSummary.nextPayout.reference}
                    </div>
                    <div className="text-xs uppercase tracking-wide text-slate-400 dark:text-slate-500">Next Payout</div>
                  </div>
                  <span className="text-base font-semibold text-slate-900 dark:text-slate-100">
                    ${financeSummary.nextPayout.amount.toFixed(2)}
                  </span>
                </div>

                <div className="mt-3 flex flex-col gap-1 text-xs text-slate-500 dark:text-slate-400">
                  {financeSummary.nextPayout.initiatedAtUtc && (
                    <span>
                      Initiated:&nbsp;
                      <strong className="font-medium text-slate-700 dark:text-slate-300">
                        {formatUtcToLocal(financeSummary.nextPayout.initiatedAtUtc, {
                          day: '2-digit',
                          month: 'short',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </strong>
                    </span>
                  )}
                  <span>Status: Pending</span>
                </div>
              </div>
            )}

            {financeSummary.previousPayout && (
              <div className="rounded-lg border border-slate-100 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 text-sm font-semibold text-slate-800 dark:text-slate-200">
                      <CalendarDays className="h-4 w-4 text-emerald-500 dark:text-emerald-400" />
                      {financeSummary.previousPayout.reference}
                    </div>
                    <div className="text-xs uppercase tracking-wide text-slate-400 dark:text-slate-500">Previous Payout</div>
                  </div>
                  <span className="text-base font-semibold text-slate-900 dark:text-slate-100">
                    ${financeSummary.previousPayout.amount.toFixed(2)}
                  </span>
                </div>

                <div className="mt-3 flex flex-col gap-1 text-xs text-slate-500 dark:text-slate-400">
                  {financeSummary.previousPayout.settledAtUtc && (
                    <span>
                      Settled:&nbsp;
                      <strong className="font-medium text-slate-700 dark:text-slate-300">
                        {formatUtcToLocal(financeSummary.previousPayout.settledAtUtc, {
                          day: '2-digit',
                          month: 'short',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </strong>
                    </span>
                  )}
                  <span>Status: Completed</span>
                </div>
              </div>
            )}

            {!financeSummary.nextPayout && !financeSummary.previousPayout && (
              <div className="text-center py-8 text-slate-500 dark:text-slate-400">No settlement history</div>
            )}
          </div>
        </section>
      </div>
      </div>

      <TopUpDialog
        isOpen={isTopUpDialogOpen}
        onClose={() => setIsTopUpDialogOpen(false)}
        onTopUp={handleTopUp}
      />

      <SettlementDialog
        isOpen={isSettlementDialogOpen}
        onClose={() => setIsSettlementDialogOpen(false)}
        onSettle={handleSettle}
        availableBalance={financeSummary.balance}
      />
    </>
  );
};

const SummaryCard = ({
  icon,
  title,
  value,
  helper,
  action,
}: {
  icon: React.ReactNode;
  title: string;
  value: string;
  helper: string;
  action?: React.ReactNode;
}) => (
  <div className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-indigo-200 dark:border-slate-700 dark:bg-slate-900 dark:hover:border-indigo-700">
    <div className="flex items-center justify-between">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-50 dark:bg-indigo-950/50">{icon}</div>
      <span className="text-xs font-medium uppercase text-slate-400 dark:text-slate-500">{title}</span>
    </div>
    <div className="space-y-1">
      <p className="text-2xl font-semibold text-slate-900 dark:text-slate-100">{value}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{helper}</p>
      {action}
    </div>
  </div>
);