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
  collectionId: string,
  fileName = "e2e.jpg",
): Promise<TestPhoto> {
  const response = await api.post(
    `/api/v1/admin/collections/${collectionId}/photos`,
    {
      multipart: {
        files: {
          name: fileName,
          mimeType: "image/jpeg",
          buffer: await readFile(FIXTURE_PHOTO),
        },
      },
    },
  );

  expect(response.status(), "photo upload").toBe(201);
  const body = (await response.json()) as {
    created: TestPhoto[];
    failed: { fileName: string; reason: string }[];
  };

  expect(body.failed, "fixture should be accepted").toHaveLength(0);
  return body.created[0]!;
}

export async function deleteCollection(
  api: APIRequestContext,
  id: string,
): Promise<void> {
  await api.delete(`/api/v1/admin/collections/${id}`);
}
