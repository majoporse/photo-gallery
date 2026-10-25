# 06 — Admin write endpoints

Status: ready-for-agent
Blocked by: 03, 04, 05

## Context
Everything the owner can change. Ordering and cascades are the parts that break silently, so they get explicit tests.

## Scope
- Collection CRUD (`POST`, `PATCH`, `DELETE` soft), `PUT /admin/collections/order`.
- `POST /admin/collections/{id}/photos` with repeated `files`, returning `{ created, failed }` and a 201 even when some files were rejected.
- `PATCH /admin/photos/{id}` for title, caption, altText, takenAt, featuredRank, collectionId.
- `DELETE /admin/photos/{id}` soft delete; `PUT /admin/collections/{id}/photos/order`.
- Deleting a collection soft-deletes its photos and leaves their objects in place.
- Slug is immutable: `PATCH` rejects it explicitly rather than ignoring it.
- Reorder is transactional and rewrites the full `0..n-1` range; moving a photo between collections closes the gap it left.
- Admin reads (`GET /admin/collections`) include soft-deleted rows.

## Done when
- Uploading three files where one is invalid returns two created and one failed, and the two are visible on the public endpoints immediately.
- Reordering a collection twice leaves contiguous positions with no duplicates.
- Moving a photo out of a collection leaves no hole, and the photo appears in its new collection at the end.
- Deleting a collection hides its photos from public endpoints but leaves both buckets untouched.
- Attempting to change a slug returns 400 with a `ProblemDetails` naming the field.
