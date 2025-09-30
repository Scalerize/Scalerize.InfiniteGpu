import { useQuery } from "@tanstack/react-query";
import { getRequestorIntake } from "../api/requestorIntakeApi";

export const useRequestorIntakeQuery = () => {
  return useQuery({
    queryKey: ["requestor-intake"],
    queryFn: getRequestorIntake,
    refetchInterval: 30000, // Refetch every 30 seconds for near real-time data
    staleTime: 20000,
  });
};