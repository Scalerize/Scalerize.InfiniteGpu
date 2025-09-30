import { useQuery } from "@tanstack/react-query";
import { getTaskSubtasks } from "../api";

export const useTaskSubtasksQuery = (taskId: string, enabled = true) => {
  return useQuery({
    queryKey: ["task-subtasks", taskId],
    queryFn: () => getTaskSubtasks(taskId),
    enabled: enabled && !!taskId,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
};