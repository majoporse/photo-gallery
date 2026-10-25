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

test("the sitemap never lists admin routes", async ({ request }) => {
  const body = await (await request.get("/sitemap.xml")).text();

  expect(body).toContain("<urlset");
  expect(body).not.toContain("/admin");
});
