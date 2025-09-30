import { PaymentsEarningsPanel } from "../features/provider/components/PaymentsEarningsPanel";
import { PageHeader } from "../shared/components/PageHeader";

export const FinancePage = () => (
  <div className="flex h-full flex-col overflow-hidden">
    <PageHeader
      title="Payments & Earnings"
      description="Reconcile provider payouts, requestor debits, and settlement cycles in a unified ledger view."
    />
    <div className="mt-6 flex-1 overflow-y-auto">
      <div className="flex flex-col gap-6 pb-6">
        <PaymentsEarningsPanel />
      </div>
    </div>
  </div>
);