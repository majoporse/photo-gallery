import { expect, test } from "@playwright/test";
import { readFile } from "node:fs/promises";
import {
  ADMIN_PASSWORD,
  ADMIN_USERNAME,
  FIXTURE_PHOTO,
  TINY_PNG,
  adminApi,
  createCollection,
  deleteCollection,
  uniqueSlug,
  uploadPhoto,
  type TestCollection,
} from "./helpers";

test("owner signs in, creates a collection, and uploads with a per-file rejection", async ({
  page,
}) => {
  const api = await adminApi();
  const suffix = Date.now().toString(36);
  const name = `E2E Admin ${suffix}`;
  const slug = `e2e-admin-${suffix}`;
  let collection: TestCollection | undefined;

  try {
    await page.goto("/admin");
    await page.getByLabel("Username").fill(ADMIN_USERNAME);
    await page.getByLabel("Password").fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      page.getByRole("heading", { level: 1, name: "The archive" }),
    ).toBeVisible();

    await page.locator("#new-name").fill(name);
    await page.locator("#new-slug").fill(slug);

    const [created] = await Promise.all([
      page.waitForResponse(
        (response) =>
          response.url().endsWith("/api/v1/admin/collections") &&
          response.request().method() === "POST",
      ),
      page.getByRole("button", { name: "Create collection" }).click(),
    ]);
    expect(created.status(), await created.text()).toBe(201);

    // Creating navigates straight to the new collection, so do not depend on
    // catching the list before the route changes.
    collection = await findCollection(api, slug);
    await page.goto(`/admin/collections/${slug}`);
    await expect(page).toHaveURL(
      new RegExp(`/admin/collections/${slug}`),
    );
    await expect(
      page.getByRole("heading", { level: 1, name }),
    ).toBeVisible();

    await page.locator("#upload-files").setInputFiles([
      {
        name: "fixture.jpg",
        mimeType: "image/jpeg",
        buffer: await readFile(FIXTURE_PHOTO),
      },
      TINY_PNG,
    ]);

    await expect(page.getByText("Added", { exact: true })).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByText("Rejected", { exact: true })).toBeVisible({
      timeout: 30_000,
    });

    // The notice says the public site picks the photos up on the next load;
    // reloading also proves the upload persisted rather than just updating state.
    await page.reload();
    await expect(page.locator("main img")).toHaveCount(1);
  } finally {
    if (collection) {
      await deleteCollection(api, collection.id);
    }

    await api.dispose();
  }
});

test("owner edits collection settings and restores a hidden collection", async ({
  page,
}) => {
  const api = await adminApi();
  const suffix = Date.now().toString(36);
  const slug = uniqueSlug(`e2e-settings-${suffix}`);
  const originalName = `Settings ${suffix}`;
  const editedName = `Settings edited ${suffix}`;
  let collection: TestCollection | undefined;

  try {
    collection = await createCollection(api, slug, originalName);
    await uploadPhoto(api, collection);

    await page.goto("/admin");
    await page.getByLabel("Username").fill(ADMIN_USERNAME);
    await page.getByLabel("Password").fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(
      page.getByRole("heading", { level: 1, name: "The archive" }),
    ).toBeVisible();

    await page.goto(`/admin/collections/${slug}/edit`);
    await expect(
      page.getByRole("heading", { level: 1, name: "Collection settings" }),
    ).toBeVisible();

    await page.getByLabel("Name").fill(editedName);
    await page
      .getByLabel("Description")
      .fill("Photographs from the edit-page test.");
    await page.getByRole("button", { name: "Save changes" }).click();
    await expect(page.getByText("Saved.")).toBeVisible();

    await page.reload();
    await expect(page.getByLabel("Name")).toHaveValue(editedName);
    await expect(page.getByLabel("Description")).toHaveValue(
      "Photographs from the edit-page test.",
    );

    await page.getByRole("button", { name: "Hide collection…" }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "Hide collection" })
      .click();
    await expect(page).toHaveURL(/\/admin\/collections$/);
    await expect(
      page.getByRole("heading", { name: "Hidden collections" }),
    ).toBeVisible();
    await expect(
      page.getByRole("listitem").filter({ hasText: slug }),
    ).toContainText("1 photo");

    await page.goto(`/admin/collections/${slug}/edit`);
    await page.getByRole("button", { name: "Restore collection" }).click();
    await expect(page.getByText("Collection restored.")).toBeVisible();
    await expect(page.getByText("Public", { exact: true })).toBeVisible();

    const response = await page.goto(`/collections/${slug}`);
    expect(response?.status()).toBe(200);
    await expect(page.locator("main img")).toHaveCount(1);
  } finally {
    if (collection) {
      await api.post(`/api/v1/admin/collections/${collection.id}/restore`);
      await deleteCollection(api, collection.id);
    }

    await api.dispose();
  }
});

async function findCollection(
  api: Awaited<ReturnType<typeof adminApi>>,
  slug: string,
): Promise<TestCollection> {
  const response = await api.get("/api/v1/admin/collections");
  const collections = (await response.json()) as {
    id: string;
    slug: string;
    deletedAt: string | null;
  }[];
  const match = collections.find((candidate) => candidate.slug === slug);
  expect(match, `collection ${slug} should exist`).toBeTruthy();
  return { id: match!.id, slug: match!.slug };
}
