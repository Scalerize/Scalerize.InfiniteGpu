import type { ReactNode } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { appQueryClient } from './queryClient';

interface QueryProviderProps {
  children: ReactNode;
}

export const QueryProvider = ({ children }: QueryProviderProps) => {
  return <QueryClientProvider client={appQueryClient}>{children}</QueryClientProvider>;
};