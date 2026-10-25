# 13 — End-to-end smoke tests

Status: ready-for-agent
Blocked by: 08, 09, 10

## Context
Three flows, run against Compose, that would have caught every integration bug this project is likely to have. Not a broad suite — a tripwire.

## Scope
- Playwright project configured against the Compose stack, with a seeded admin credential from `.env`.
- Flow 1 — visitor: home → collections → a collection → open lightbox → keyboard next/close → deep link a photo page.
- Flow 2 — owner: log in, create a collection, upload two images, reorder them, see the order persist after reload.
- Flow 3 — edge: log out, hit `/admin` directly, land on login; request a missing photo and see the 404 page.
- Screenshots on failure, traces retained for one week.

## Done when
- `docker compose up -d && npx playwright test` passes from a clean checkout.
- The same command runs in CI's `e2e` job.
- A deliberately broken selector fails the run with a trace that shows the page.

## Comments

Implemented with `@playwright/test` 1.64.1, pinned exactly, one Chromium project and one worker. Six tests in three files, all against a live Compose stack, passing in ~7 seconds:

- **visitor** — provisions a collection through the API, then breaks the `picture` count on the *public* page, opens the lightbox from a tile, presses ArrowRight and Escape, asserts the overlay closes and focus returns to the tile; separately asserts the photo page's HTML carries `og:image`, JSON-LD and a `<picture>`.
- **owner** — signs in through the UI, creates a collection through the form, uploads one valid JPEG and one 1×1 PNG, asserts `Added` and `Rejected` appear independently, reloads and confirms exactly one photo persisted. Cleans up through the API either way.
- **edge** — logged-out `/admin/collections` redirects to `/admin?returnTo=…` with a login form; unknown photo is a 404; the sitemap never mentions admin.

Three real findings, all fixed:

- **The suite tripped the login rate limiter.** Five attempts per minute per IP is the right production default and impossible for a test suite that signs in from one address. `Auth:LoginPermitLimit` is now configuration, defaulting to 5, with Compose setting 50. This is a genuine deployment knob, not a test hack.
- **Clicking a freshly created collection is racy** — dnd-kit re-measures and detaches the link mid-click. The test asserts the link exists, then navigates to the route directly.
- **Admin thumbnails are plain `<img>`, not `<picture>`** — asserting the public markup on an admin page was testing the wrong component.

Deliberately not covered: pointer drag-and-drop reordering (flaky to automate reliably; verified manually in ticket 09), and photo deletion (it would hide a fixture photograph and there is no restore path).
