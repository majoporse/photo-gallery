import { QueryCache, QueryClient } from "@tanstack/react-query";

export const PUBLIC_STALE_TIME = 60_000;

export interface QueryClientOptions {
  retry?: number;
  onQueryError?: (error: unknown) => void;
}

export function makeQueryClient(
  staleTime = PUBLIC_STALE_TIME,
  { retry = 1, onQueryError }: QueryClientOptions = {},
): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime,
        gcTime: 5 * 60_000,
        retry,
        refetchOnWindowFocus: false,
      },
    },
    queryCache: onQueryError
      ? new QueryCache({ onError: onQueryError })
      : undefined,
  });
}
