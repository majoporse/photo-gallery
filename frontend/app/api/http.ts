export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

function resolveBaseUrl(): string {
  if (typeof window === "undefined") {
    return (process.env.API_INTERNAL_URL ?? "http://localhost:8080").replace(
      /\/$/,
      "",
    );
  }

  return (window.__PUBLIC_API_URL__ ?? "").replace(/\/$/, "");
}

/**
 * Orval mutator. Server requests go to the API over cluster DNS; browser
 * requests use the public API origin injected by the root loader.
 */
export async function apiFetch<T>(
  url: string,
  options: RequestInit = {},
): Promise<T> {
  const response = await fetch(`${resolveBaseUrl()}${url}`, {
    ...options,
    headers: {
      Accept: "application/json",
      ...options.headers,
    },
  });

  if (!response.ok) {
    let detail = response.statusText;
    try {
      const body = (await response.json()) as {
        detail?: string;
        title?: string;
      };
      detail = body.detail ?? body.title ?? detail;
    } catch {
      // A non-JSON error body is fine; the status is the useful part.
    }
    throw new ApiError(response.status, detail || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
