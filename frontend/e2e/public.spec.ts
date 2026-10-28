import { expect, test, type APIRequestContext } from "@playwright/test";
import {
  adminApi,
  createCollection,
  deleteCollection,
  uniqueSlug,
  uploadPhoto,
  type TestCollection,
  type TestPhoto,
} from "./helpers";

test.describe("visitor", () => {
  let api: APIRequestContext;
  let collection: TestCollection;
  let first: TestPhoto;

  test.beforeAll(async () => {
    api = await adminApi();
    collection = await createCollection(
      api,
      uniqueSlug("e2e-public"),
      "E2E Public",
    );
    first = await uploadPhoto(api, collection, "first.jpg");
    await uploadPhoto(api, collection, "second.jpg");
  });

  test.afterAll(async () => {
    await deleteCollection(api, collection.id);
    await api.dispose();
  });

  test("grid renders pictures and the lightbox is keyboard operable", async ({
    page,
  }) => {
    await page.goto(`/collections/${collection.slug}`);

    await expect(page.getByRole("heading", { level: 1 })).toContainText(
      "E2E Public",
    );
    await expect(page.locator("picture")).toHaveCount(2);

    const tile = page.locator(`#photo-tile-${first.id}`);
    await expect(tile).toBeVisible();
    await tile.click();

    const lightbox = page.locator(".yarl__root");
    await expect(lightbox).toBeVisible();

    await page.keyboard.press("ArrowRight");
    await page.keyboard.press("Escape");

    await expect(lightbox).toBeHidden();
    await expect(tile).toBeFocused();
  });

  test("photo page is server-rendered with social metadata", async ({
    request,
  }) => {
    const response = await request.get(`/photos/${first.id}`);

    expect(response.status()).toBe(200);
    const html = await response.text();
    expect(html).toContain('property="og:image"');
    expect(html).toContain('"@type":"ImageObject"');
    expect(html).toContain("<picture");
  });
});
