import { SubtaskList } from "../features/provider/components/SubtaskList";
import { PageHeader } from "../shared/components/PageHeader";

export const TasksPage = () => (
  <div className="flex h-full flex-col overflow-hidden">
    <PageHeader
      title="Provider Tasks"
      description="Detailed queue of subtasks, execution environments, and lifecycle events for compute providers."
    />
    <div className="mt-6 flex-1 overflow-y-auto">
      <div className="flex flex-col gap-6 pb-6">
        <SubtaskList />
      </div>
    </div>
  </div>
);
