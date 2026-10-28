import { ApiError } from "~/api/http";
import type {
  CollectionAdminView,
  CreateCollection,
  EditPhoto,
  HomeFeed,
  PagedResultOfPhotoSummary,
  PhotoDetail,
  PhotoSummary,
  PresignedUpload,
  SessionResponse,
  UpdateCollection,
  UploadView,
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

export function restoreCollection(id: string): Promise<CollectionAdminView> {
  return request<CollectionAdminView>(
    `/api/v1/admin/collections/${encodeURIComponent(id)}/restore`,
    { method: "POST" },
  );
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
  loaded: number;
  total: number;
}

/**
 * Two calls and one direct transfer: the API reserves a position and signs a
 * URL, the browser writes the bytes, and the API is told the file landed. The
 * API never holds the file, so the request is short whatever the file weighs.
 */
export function beginUpload(
  collectionId: string,
  fileName: string,
  bytes: number,
): Promise<PresignedUpload> {
  return request<PresignedUpload>(
    `/api/v1/admin/collections/${encodeURIComponent(collectionId)}/uploads`,
    {
      method: "POST",
      body: JSON.stringify({ fileName, bytes }),
    },
  );
}

/**
 * XMLHttpRequest rather than fetch because only XHR reports upload progress.
 * No credentials and no custom headers: object storage authenticates by the
 * signature in the URL, and an extra header would only widen the preflight.
 */
export function putFile(
  url: string,
  file: File,
  onProgress: (progress: UploadProgress) => void,
): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", url);

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress({ loaded: event.loaded, total: event.total });
      }
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve();
        return;
      }

      reject(
        new AdminApiError(
          xhr.status,
          storageError(xhr.responseText) ||
            `Object storage refused the file (HTTP ${xhr.status}).`,
        ),
      );
    };

    xhr.onerror = () => {
      reject(new Error("The upload could not reach object storage."));
    };

    xhr.onabort = () => {
      reject(new Error("The upload was cancelled."));
    };

    xhr.send(file);
  });
}

export function completeUpload(uploadId: string): Promise<UploadView> {
  return request<UploadView>(
    `/api/v1/admin/uploads/${encodeURIComponent(uploadId)}/complete`,
    { method: "POST" },
  );
}

export interface UploadStreamHandlers {
  onSnapshot: (uploads: UploadView[]) => void;
  onUpdate: (upload: UploadView) => void;
}

/**
 * The stream opens with a snapshot and then carries deltas, so a reconnect
 * resynchronises without a second request and without a gap between them.
 */
export function openUploadStream(handlers: UploadStreamHandlers): () => void {
  const source = new EventSource(`${baseUrl()}/api/v1/admin/uploads/events`, {
    withCredentials: true,
  });

  const read = (event: Event): unknown =>
    JSON.parse((event as MessageEvent<string>).data) as unknown;

  source.addEventListener("snapshot", (event) => {
    handlers.onSnapshot(read(event) as UploadView[]);
  });

  source.addEventListener("upload.updated", (event) => {
    handlers.onUpdate(read(event) as UploadView);
  });

  return () => source.close();
}

/** S3 answers failures with a small XML document; the message is the useful part. */
function storageError(body: string): string | null {
  const match = /<Message>([^<]+)<\/Message>/.exec(body);
  return match?.[1] ?? null;
}

export const adminKeys = {
  all: ["admin"] as const,
  collections: ["admin", "collections"] as const,
  collection: (slug: string) => ["admin", "collection", slug] as const,
  photo: (id: string) => ["admin", "photo", id] as const,
  home: ["admin", "home"] as const,
};
