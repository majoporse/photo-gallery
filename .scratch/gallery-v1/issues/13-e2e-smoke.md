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
