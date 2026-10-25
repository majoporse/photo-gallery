import { ApiError } from "~/api/http";

export function rethrowAs404(error: unknown): never {
  if (error instanceof ApiError && error.status === 404) {
    throw new Response(null, { status: 404 });
  }

  throw error;
}
