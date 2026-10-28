# 17 — Admin: restore a hidden collection

Status: ready-for-agent
Blocked by: none

## Context
Hiding a collection is currently a one-way door. `DELETE /admin/collections/{id}` soft-deletes the collection and cascades `DeletedAt` to every photo that is live at that moment (`AdminGallery.DeleteCollectionAsync`). The admin list already separates `hiddenCollections` and shows a "Hidden" badge, but tells the owner the API has no restore action, and `/admin/collections/:slug` says the same. Storage is untouched, so everything needed to undo the hide is still there; only the command and the UI are missing.

Restoring must undo exactly that hide: bring the collection back, and bring back the photographs the cascade hid with it. Photographs that had already been hidden individually before the collection was hidden must stay hidden. A collection whose slug was reused by a live collection while it was hidden cannot be restored without creating a duplicate public URL.

## Scope

Backend
- Add `POST /api/v1/admin/collections/{id}/restore` to `AdminGalleryEndpoints`. The admin group already supplies the session and CSRF guard. Return `200` with `CollectionAdminView`, `404` for an unknown id, and `409` when a live collection now owns the slug.
- Add `IAdminGallery.RestoreCollectionAsync` and the `AdminGallery` implementation. Look the collection up with `IgnoreQueryFilters`, check for a live collection with the same slug (the unique index is partial on `"DeletedAt" IS NULL`, so `CreateCollectionAsync` can reuse a hidden slug), then in one transaction:
  - capture the collection's `DeletedAt` before clearing it;
  - clear the collection's `DeletedAt`, set `UpdatedAt = now`, and append it after the last live collection (`max(Position) + 1`, or `0` when none) so two live collections never share a position;
  - restore photos in that collection whose `DeletedAt` equals the captured cascade timestamp exactly, and only those. `DeleteCollectionAsync` writes one `now` to the collection and every live photo; an individually hidden photo carries an earlier timestamp. Do not use `DeletedAt is not null`, which would resurrect unrelated deletions.
- Restoring an already-live collection is an idempotent success: return its current view and touch nothing.
- Put the state change on the aggregate (`Collection.Restore(...)`) rather than assigning `DeletedAt` from Infrastructure. Leave a short comment explaining that the timestamp is the cascade's identity, not just a date.
- Regenerate `contracts/openapi.json`. The CI drift check and the Orval client both depend on it.
- Tests in `AdminGalleryTests`:
  - a hidden collection and its cascade-hidden photos become visible again, with their photo order preserved;
  - a photo hidden individually before the collection was hidden stays hidden after restore;
  - a hidden collection whose slug was reused returns the conflict and leaves the collection hidden;
  - restoring a live collection is a no-op;
  - the restored collection appears at the end of the live order, and its public detail and photo endpoints return 200 again.
- Update the admin API table in `spec.md` and the route notes in `frontend/README.md`.

Frontend
- Add `restoreCollection(id): Promise<CollectionAdminView>` to `frontend/app/api/admin.ts`, using the regenerated model.
- Give `HiddenCollectionRow` in `frontend/app/routes/admin-collections.tsx` a Restore button with a pending state and an inline error. On success invalidate `adminKeys.collections` and `adminKeys.home`.
- Update the copy that currently says restore is impossible: the hidden section, the hide confirmation ("this screen cannot restore them"), and the hidden notice in `/admin/collections/:slug`. Point the last one at the restore action instead.
- Add Vitest coverage for any extracted helper.

## Done when
- Hiding a collection, then restoring it, brings back the collection and the photos it hid; `/collections/{slug}` and its photo list return 200 with the original photo order.
- A photo hidden before the collection was hidden is still absent after restore.
- Restore returns 409 and leaves the hidden collection unchanged when a live collection has taken its slug.
- Restore is idempotent on a live collection.
- The hidden list exposes the action, shows progress and errors, and a reload reflects the new state.
- `dotnet test`, the contract drift check, `npm run typecheck`, `npm run lint`, and `npm run test` pass.

## Comments

Implemented. `POST /api/v1/admin/collections/{id}/restore` returns `CollectionAdminView`, 404 for an unknown id and 409 when a live collection owns the slug. `AdminGallery.RestoreCollectionAsync` restores the collection at the end of the live order and the photos whose `DeletedAt` equals the cascade timestamp; individually hidden photos stay hidden. The restore is idempotent on a live collection.

`CollectionAdminView` also gained `createdAt` and `updatedAt` for the settings page. Four new `AdminGalleryTests` cover cascade restore, the individual-photo exclusion, slug reuse and the live no-op; full backend suite, frontend checks and the admin Playwright flow pass.
