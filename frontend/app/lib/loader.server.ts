import { dehydrate, type QueryClient } from "@tanstack/react-query";
import { fixturesEnabled } from "./env.server";
import { makeQueryClient, PUBLIC_STALE_TIME } from "./query";

export function publicQueryClient(): QueryClient {
  return makeQueryClient(
    fixturesEnabled() ? Number.POSITIVE_INFINITY : PUBLIC_STALE_TIME,
  );
}

export function dehydrated(queryClient: QueryClient) {
  return dehydrate(queryClient);
}

export function parsePage(value: string | null): number {
  if (!value) {
    return 1;
  }

  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}
