# 18 — Admin: a collection settings page that edits everything

Status: ready-for-agent
Blocked by: 17

## Context
Collection fields can only be changed in a small inline form inside the `/admin/collections` list ("Edit"), and that form exposes only name and description. Clicking a collection opens its photo grid, where the collection's own fields are read-only. There is no single page where the owner can inspect a collection and change its settings; the only place collection fields exist is the create form and the list row. The owner wants an edit page in the admin area, not create-only fields.

`spec.md` makes the slug permanent because it is the public URL and may be indexed. "Everything" therefore means every editable field: name, description, visibility, and position. Slug, timestamps, photo count and the derived cover stay read-only and explained.

## Scope
- Add `/admin/collections/:slug/edit` under the admin layout in `frontend/app/routes.ts`. The existing `/admin/collections/:slug` stays the photo-management screen.
- Load the collection through `listCollections` and find it by slug, matching `admin-collection.tsx`; that endpoint already returns hidden collections. An unknown slug becomes the same `AdminApiError(404)` the detail page uses. The shared admin layout already handles 401.
- Form contents:
  - Name (required, trimmed) and description (optional, empty becomes `null`) through the existing `updateCollection` PATCH.
  - Slug as read-only text with the permanent-URL explanation.
  - Visibility: "Public" or "Hidden", with Hide (confirmation, existing `deleteCollection`) and Restore (`restoreCollection` from ticket 17).
  - Position: show where the collection sits in the public order and link back to `/admin/collections` for drag reordering. Do not add a numeric position input: the reorder endpoint requires the complete ordered id set.
  - Photo count, created/updated timestamps, the public URL, and links to the public collection and the photo-management screen. The cover follows the first photo, so point at photo ordering rather than adding a cover field.
- After a successful save, invalidate `adminKeys.collections` and `adminKeys.collection(slug)`. After hide or restore, also invalidate `adminKeys.home`. Navigate to `/admin/collections` after hiding (the edit page is for a hidden collection); stay on the page after restoring and after saving.
- Make the page discoverable from the admin area: the list row's inline editor becomes an Edit link to this page (remove the now-dead `editing`/`save` state from `SortableCollectionRow`), the detail page header gets an "Edit collection" link or a Photographs / Settings tab pair, and hidden rows link here too.
- Out of scope: changing the slug. It would invalidate permanent public URLs and needs a redirect strategy and an ADR; a separate ticket should decide that.
- Update the route table in `frontend/README.md`.
- Tests:
  - Vitest for any extracted form/payload helper.
  - Playwright: edit name and description from the page, reload, values persist; hide from the page and confirm the public page 404s; restore from the page and confirm it returns 200; the slug is rendered as text, not an input.

## Done when
- `/admin/collections/:slug/edit` is reachable from the collection list and from the collection's photo screen.
- Name and description save, survive a reload, and the public collection page reflects them.
- The page shows slug, status, position, photo count and timestamps accurately, including for a hidden collection.
- Hide and Restore work from the page, with progress and error states; restoring makes the collection and its cascade-hidden photos public again.
- There is no second inline editor left on the list.
- `npm run typecheck`, `npm run lint`, `npm run test`, and the admin Playwright flow pass.

## Comments

Implemented as `/admin/collections/:slug/edit` (`frontend/app/routes/admin-collection-settings.tsx`). The list's inline editor is gone; its Edit action and the collection detail header link to the settings page. The page edits name and description, shows the permanent slug as text, and manages visibility with Hide/Restore. It shows rank among live collections (not the raw position, which can have gaps), photo count, created/updated timestamps and public links.

`CollectionAdminView` gained `createdAt` and `updatedAt` to support the metadata panel. The admin Playwright flow edits and reloads the fields, hides the collection, restores it from the settings page and checks the public page and its photo render again.
