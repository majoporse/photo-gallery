import { ApiError } from "~/api/http";
import type {
  CollectionAdminView,
  CreateCollection,
  EditPhoto,
  HomeFeed,
  PagedResultOfPhotoSummary,
  PhotoDetail,
  PhotoSummary,
  SessionResponse,
  UpdateCollection,
  UploadBatch,
} from "~/api/generated/model";

/**
 * The admin API lives on a different origin from the site and authenticates
 * with an HttpOnly cookie, so every request opts into credentials and every
 * mutation carries the header the CSRF guard requires.
 */
export const ADMIN_REQUEST_HEADER = "X-Requested-With";
export const ADMIN_REQUEST_HEADER_VALUE = "photo-gallery";

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

export class AdminApiError extends ApiError {
  readonly errors: Record<string, string[]>;

  constructor(
    status: number,
    message: string,
    errors: Record<string, string[]> = {},
  ) {
    super(status, message);
    this.name = "AdminApiError";
    this.errors = errors;
  }
}

function baseUrl(): string {
  return (window.__PUBLIC_API_URL__ ?? "").replace(/\/$/, "");
}

function parseErrorBody(
  status: number,
  body: string,
  fallback: string,
): AdminApiError {
  let detail = fallback;
  let errors: Record<string, string[]> = {};

  try {
    const problem = JSON.parse(body) as {
      detail?: string | null;
      title?: string | null;
      errors?: Record<string, string[]>;
    };
    detail = problem.detail ?? problem.title ?? detail;
    if (problem.errors) {
      errors = problem.errors;
    }
  } catch {
    // A non-JSON body is common for rate limiting; the status is the useful part.
  }

  return new AdminApiError(status, detail || `HTTP ${status}`, errors);
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  if (!SAFE_METHODS.has(method)) {
    headers.set(ADMIN_REQUEST_HEADER, ADMIN_REQUEST_HEADER_VALUE);
  }

  if (typeof init.body === "string" && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${baseUrl()}${path}`, {
    ...init,
    method,
    headers,
    credentials: "include",
  });

  if (!response.ok) {
    const text = await response.text();
    throw parseErrorBody(response.status, text, response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function adminSession(): Promise<SessionResponse> {
  return request<SessionResponse>("/api/v1/admin/session");
}

export function adminLogin(username: string, password: string): Promise<void> {
  return request<void>("/api/v1/admin/session", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  });
}

export function adminLogout(): Promise<void> {
  return request<void>("/api/v1/admin/session", { method: "DELETE" });
}

export function listCollections(): Promise<CollectionAdminView[]> {
  return request<CollectionAdminView[]>("/api/v1/admin/collections");
}

export function createCollection(
  input: CreateCollection,
): Promise<CollectionAdminView> {
  return request<CollectionAdminView>("/api/v1/admin/collections", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function updateCollection(
  id: string,
  input: UpdateCollection,
): Promise<CollectionAdminView> {
  return request<CollectionAdminView>(
    `/api/v1/admin/collections/${encodeURIComponent(id)}`,
    { method: "PATCH", body: JSON.stringify(input) },
  );
}

export function deleteCollection(id: string): Promise<void> {
  return request<void>(`/api/v1/admin/collections/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function reorderCollections(
  ids: readonly string[],
): Promise<CollectionAdminView[]> {
  return request<CollectionAdminView[]>("/api/v1/admin/collections/order", {
    method: "PUT",
    body: JSON.stringify({ ids }),
  });
}

export function editPhoto(id: string, input: EditPhoto): Promise<PhotoSummary> {
  return request<PhotoSummary>(`/api/v1/admin/photos/${encodeURIComponent(id)}`, {
    method: "PATCH",
    body: JSON.stringify(input),
  });
}

export function deletePhoto(id: string): Promise<void> {
  return request<void>(`/api/v1/admin/photos/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

export function reorderPhotos(
  collectionId: string,
  ids: readonly string[],
): Promise<PhotoSummary[]> {
  return request<PhotoSummary[]>(
    `/api/v1/admin/collections/${encodeURIComponent(collectionId)}/photos/order`,
    { method: "PUT", body: JSON.stringify({ ids }) },
  );
}

/** Public reads are enough for the admin grid; there is no admin photo list. */
export function getHome(): Promise<HomeFeed> {
  return request<HomeFeed>("/api/v1/home");
}

export function getPhotoDetail(id: string): Promise<PhotoDetail> {
  return request<PhotoDetail>(`/api/v1/photos/${encodeURIComponent(id)}`);
}

export async function listCollectionPhotos(
  slug: string,
): Promise<PhotoSummary[]> {
  const photos: PhotoSummary[] = [];
  let page = 1;
  let totalPages = 1;

  do {
    const result = await request<PagedResultOfPhotoSummary>(
      `/api/v1/collections/${encodeURIComponent(slug)}/photos?page=${page}&pageSize=100`,
    );
    photos.push(...result.items);
    totalPages = Math.max(
      1,
      Math.ceil(Number(result.total) / Number(result.pageSize)),
    );
    page += 1;
  } while (page <= totalPages);

  return photos;
}

export interface UploadProgress {
  fileIndex: number;
  loaded: number;
  total: number;
}

/**
 * XMLHttpRequest rather than fetch because only XHR reports upload progress.
 * `files` is normally one file: sequential requests keep per-file progress
 * exact and avoid two uploads computing the same next position at once.
 */
export function uploadPhotos(
  collectionId: string,
  files: readonly File[],
  onProgress?: (progress: UploadProgress) => void,
): Promise<UploadBatch> {
  const form = new FormData();
  for (const file of files) {
    form.append("files", file);
  }

  const sizes = files.map((file) => file.size);
  const totalBytes = sizes.reduce((sum, size) => sum + size, 0);

  return new Promise<UploadBatch>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(
      "POST",
      `${baseUrl()}/api/v1/admin/collections/${encodeURIComponent(collectionId)}/photos`,
    );
    xhr.withCredentials = true;
    xhr.setRequestHeader(ADMIN_REQUEST_HEADER, ADMIN_REQUEST_HEADER_VALUE);
    xhr.setRequestHeader("Accept", "application/json");

    xhr.upload.onprogress = (event) => {
      if (!onProgress || !event.lengthComputable) {
        return;
      }

      let offset = 0;
      for (let index = 0; index < files.length; index += 1) {
        const size = sizes[index] ?? 0;
        const loaded = Math.min(Math.max(event.loaded - offset, 0), size);
        onProgress({ fileIndex: index, loaded, total: size });
        offset += size;
      }
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          resolve(JSON.parse(xhr.responseText) as UploadBatch);
        } catch {
          reject(
            new AdminApiError(
              xhr.status,
              "The API returned an unreadable upload response.",
            ),
          );
        }
        return;
      }

      reject(
        parseErrorBody(
          xhr.status,
          xhr.responseText,
          xhr.statusText ||
            `Upload failed before all ${totalBytes} bytes were accepted.`,
        ),
      );
    };

    xhr.onerror = () => {
      reject(new Error("The upload could not reach the API."));
    };

    xhr.onabort = () => {
      reject(new Error("The upload was cancelled."));
    };

    xhr.send(form);
  });
}

export const adminKeys = {
  all: ["admin"] as const,
  collections: ["admin", "collections"] as const,
  collection: (slug: string) => ["admin", "collection", slug] as const,
  photo: (id: string) => ["admin", "photo", id] as const,
  home: ["admin", "home"] as const,
};
