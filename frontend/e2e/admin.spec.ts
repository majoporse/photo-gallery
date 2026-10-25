import { expect, test } from "@playwright/test";
import { readFile } from "node:fs/promises";
import {
  ADMIN_PASSWORD,
  ADMIN_USERNAME,
  FIXTURE_PHOTO,
  TINY_PNG,
  adminApi,
  deleteCollection,
  uniqueSlug,
  type TestCollection,
} from "./helpers";

test("owner signs in, creates a collection, and uploads with a per-file rejection", async ({
  page,
}) => {
  const api = await adminApi();
  const slug = uniqueSlug("e2e-admin");
  let collection: TestCollection | undefined;

  try {
    await page.goto("/admin");
    await page.getByLabel("Username").fill(ADMIN_USERNAME);
    await page.getByLabel("Password").fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      page.getByRole("heading", { level: 1, name: "The archive" }),
    ).toBeVisible();

    await page.locator("#new-name").fill("E2E Admin");
    await page.locator("#new-slug").fill(slug);
    await page.getByRole("button", { name: "Create collection" }).click();

    // The list re-renders as dnd-kit re-measures, so clicking the fresh link is
    // racy; assert it exists, then open the detail route directly.
    await expect(
      page.getByRole("link", { name: "E2E Admin" }).first(),
    ).toBeVisible();
    await page.goto(`/admin/collections/${slug}`);
    await expect(page).toHaveURL(new RegExp(`/admin/collections/${slug}`));

    collection = await findCollection(api, slug);

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
