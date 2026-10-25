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
