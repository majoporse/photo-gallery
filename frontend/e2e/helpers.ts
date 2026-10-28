import { expect, request, type APIRequestContext } from "@playwright/test";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

export const API_URL = process.env.E2E_API_URL ?? "http://localhost:8080";
export const ADMIN_USERNAME = process.env.E2E_ADMIN_USERNAME ?? "admin";
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? "dev-password";

export const FIXTURE_PHOTO = fileURLToPath(
  new URL("./fixtures/photo.jpg", import.meta.url),
);

/** 1×1 PNG: rejected for being shorter than 640 px, which exercises partial failure. */
export const TINY_PNG = {
  name: "tiny.png",
  mimeType: "image/png",
  buffer: Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
    "base64",
  ),
};

const CSRF_HEADER = { "X-Requested-With": "photo-gallery" };

export interface TestCollection {
  id: string;
  slug: string;
}

export interface TestPhoto {
  id: string;
  sha256: string;
}

/** A signed-in API client; the session cookie is kept in the context. */
export async function adminApi(): Promise<APIRequestContext> {
  const api = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: CSRF_HEADER,
  });

  const response = await api.post("/api/v1/admin/session", {
    data: { username: ADMIN_USERNAME, password: ADMIN_PASSWORD },
  });

  expect(response.status(), "admin login should succeed").toBe(204);
  return api;
}

export function uniqueSlug(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}`;
}

export async function createCollection(
  api: APIRequestContext,
  slug: string,
  name: string,
): Promise<TestCollection> {
  const response = await api.post("/api/v1/admin/collections", {
    data: { name, slug },
  });

  expect(response.status(), "collection creation").toBe(201);
  return (await response.json()) as TestCollection;
}

export async function uploadPhoto(
  api: APIRequestContext,
  collection: TestCollection,
  fileName = "e2e.jpg",
): Promise<TestPhoto> {
  const before = await countPhotos(api, collection.slug);
  const buffer = await readFile(FIXTURE_PHOTO);

  const begin = await api.post(
    `/api/v1/admin/collections/${collection.id}/uploads`,
    { data: { fileName, bytes: buffer.length } },
  );
  expect(begin.status(), "upload presign").toBe(201);

  const ticket = (await begin.json()) as { uploadId: string; url: string };

  // The bytes go straight to object storage, exactly as the browser sends them.
  const put = await api.fetch(ticket.url, {
    method: "PUT",
    data: buffer,
    headers: { "Content-Type": "image/jpeg" },
  });
  expect(put.status(), "direct upload").toBe(200);

  const complete = await api.post(
    `/api/v1/admin/uploads/${ticket.uploadId}/complete`,
  );
  expect(complete.status(), "upload complete").toBe(202);

  // Processing is asynchronous: wait for the Photo rather than for a response.
  let photo: TestPhoto | undefined;

  await expect
    .poll(
      async () => {
        const response = await api.get(
          `/api/v1/collections/${collection.slug}/photos?page=1&pageSize=100`,
        );
        if (!response.ok()) {
          return before;
        }

        const body = (await response.json()) as { items: TestPhoto[] };
        photo = body.items.at(-1);
        return body.items.length;
      },
      { timeout: 60_000, intervals: [500, 1000, 2000] },
    )
    .toBeGreaterThan(before);

  if (!photo) {
    throw new Error(`${fileName} was accepted but never appeared in the collection.`);
  }

  return photo;
}

async function countPhotos(
  api: APIRequestContext,
  slug: string,
): Promise<number> {
  const response = await api.get(
    `/api/v1/collections/${slug}/photos?page=1&pageSize=100`,
  );

  if (!response.ok()) {
    return 0;
  }

  const body = (await response.json()) as { items: unknown[] };
  return body.items.length;
}

export async function deleteCollection(
  api: APIRequestContext,
  id: string,
): Promise<void> {
  await api.delete(`/api/v1/admin/collections/${id}`);
}
