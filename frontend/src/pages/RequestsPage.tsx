import { RequestHistoryPanel } from "../features/requestor/components/RequestHistoryPanel";
import { PageHeader } from "../shared/components/PageHeader";

export const RequestsPage = () => (
  <div className="flex h-full flex-col overflow-hidden">
    <PageHeader
      title="Requests"
      description="Manage requestor submissions, partition compilation, and dependency resolution workflows."
    />
    <div className="mt-6 flex-1 overflow-y-auto">
      <div className="pb-6">
        <RequestHistoryPanel />
      </div>
    </div>
  </div>
);