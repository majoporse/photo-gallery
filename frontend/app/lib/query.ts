import { QueryClient } from "@tanstack/react-query";

export const PUBLIC_STALE_TIME = 60_000;

export function makeQueryClient(staleTime = PUBLIC_STALE_TIME): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime,
        gcTime: 5 * 60_000,
        retry: 1,
        refetchOnWindowFocus: false,
      },
    },
  });
}
