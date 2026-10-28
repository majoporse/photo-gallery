import { expect, test } from "@playwright/test";

test("admin screens require a session and remember where you were going", async ({
  page,
}) => {
  await page.goto("/admin/collections");

  await expect(page).toHaveURL(/\/admin\?returnTo=/);
  await expect(page.getByLabel("Username")).toBeVisible();
  await expect(page.getByLabel("Password")).toBeVisible();
});

test("unknown photos are a real 404", async ({ request }) => {
  const response = await request.get("/photos/does-not-exist");

  expect(response.status()).toBe(404);
});

test("the sitemap never lists admin routes or the API reference", async ({
  request,
}) => {
  const body = await (await request.get("/sitemap.xml")).text();

  expect(body).toContain("<urlset");
  expect(body).not.toContain("/admin");
  expect(body).not.toContain("/api-docs");
});

test("the API reference is public but noindex", async ({ page, request }) => {
  expect((await request.get("/openapi.json")).status()).toBe(200);

  const response = await request.get("/api-docs");
  expect(response.status()).toBe(200);
  expect(await response.text()).toContain('content="noindex, nofollow"');

  await page.goto("/api-docs");
  await expect(page.getByText("Download OpenAPI Document").first()).toBeVisible();
});
