# 09 — Admin UI

Status: ready-for-agent
Blocked by: 06, 07

## Context
The owner's only interface. It exists to upload and reorder photographs quickly, not to be pretty.

## Scope
- `/admin/login` — posts to the session endpoint; on success navigates to collections. Shows the rate-limit response honestly.
- `/admin/collections` — list, create, rename, delete (with an explicit "photos will be hidden, not deleted" confirmation), drag to reorder.
- `/admin/collections/:slug` — photo grid, multi-file upload with per-file progress and per-file failure reasons, drag to reorder, set featured rank.
- `/admin/photos/:id` — edit title, caption, altText, takenAt, collection; soft delete with an undo affordance that simply re-sets the field if the API supports it, otherwise a confirmation dialog that says it cannot be undone from the UI.
- 401 anywhere redirects to login; the admin `QueryClient` has `staleTime: 0`.
- All admin routes are `clientLoader`-only; no admin data is rendered on the server.

## Done when
- Uploading three photos shows three progress rows, one of which fails with a readable reason, and the successful two are on the public site immediately.
- Dragging a photo to a new position persists across a reload.
- A deep link to an admin route while logged out lands on login and returns to the intended route after signing in.
- Nothing admin-related appears in the server-rendered HTML for a logged-out request (verified by `curl`).

## Comments

Implemented. I re-ran the gates: lint clean, `tsc --noEmit` clean, 49 Vitest tests, production build.

Verified on the real stack, including the security-relevant parts:

- Mutations without the cookie → 401; with the cookie but without `X-Requested-With` → 400 naming the header; with both → created. That is the guard doing its job, not the client's promise.
- `returnTo` is restricted to `/admin/collections` and `/admin/photos` by regex, so a crafted link cannot bounce off-site.
- Logged-out `/admin` HTML contains no collection names; every admin response carries `noindex, nofollow`; the sitemap has zero admin URLs.
- A full browser flow: deep link → login → returned to the intended route; create collection; upload three files where one is rejected with "Shorter than 640 px on the long edge"; drag reorder persisting across reload; edit and feature a photo with the public page updating immediately; hide a collection and watch its public page 404; logout; and the rate limiter surfacing as "too many attempts" rather than "wrong password".

Deviations and gaps:

- **Login lives at `/admin`**, not a separate `/admin/login`; `?returnTo=` handles the deep-link case.
- **`featuredRank` is not exposed by any read endpoint**, so the edit form derives the current position from the home feed's ordering and explains that in the UI. Adding `featuredRank` to `PhotoSummary` is the honest fix and is worth doing before the admin gets more complex.
- **There is no admin photo listing**, so a soft-deleted photo cannot be seen or restored. Photo delete is confirmation-only, and was implemented but not exercised end-to-end to avoid hiding a seed photograph.
- **Uploads go one file per request** so per-file progress is exact; the API still receives the `files` field and partial failures stay isolated.
- Verification left two soft-deleted test collections in the dev database. They are invisible publicly and there is no hard-delete API.
