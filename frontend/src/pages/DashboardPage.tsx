import { ProviderThroughputCard } from "../features/provider/components/ProviderThroughputCard";
import { RequestorIntakeCard } from "../features/requestor/components/RequestorIntakeCard";
import { PageHeader } from "../shared/components/PageHeader";

export const DashboardPage = () => (
  <div className="flex h-full flex-col overflow-hidden">
    <PageHeader
      title="Dashboard"
      description="Monitor provider throughput, requestor demand, and financial reconciliation."
    />

    <div className="mt-6 flex-1 overflow-y-auto">
      <section className="grid gap-6 pb-6 xl:grid-cols-2">
        <ProviderThroughputCard />
        <RequestorIntakeCard />
      </section>
    </div>
  </div>
);