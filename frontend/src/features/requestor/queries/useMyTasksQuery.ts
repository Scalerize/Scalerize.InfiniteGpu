import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../auth/stores/authStore";
import { getMyTasks } from "../api";
import type { RequestorTaskStatus } from "../types";

const BASE_QUERY_KEY = ["requestor", "tasks", "my"] as const;

export const useMyTasksQuery = (status?: RequestorTaskStatus) => {
  const user = useAuthStore((state) => state.user);

  const queryKey =
    typeof status === "number"
      ? [...BASE_QUERY_KEY, status]
      : BASE_QUERY_KEY;

  return useQuery({
    queryKey,
    queryFn: () => getMyTasks(status),
    enabled: !!user && user.role === "Requestor",
    refetchInterval: 60000, // Poll every 1 minute
    staleTime: 1000 * 30,
  });
};

export const invalidateMyTasksQueryKey = BASE_QUERY_KEY;