# 04 — Public read endpoints

Status: ready-for-agent
Blocked by: 02

## Context
The five endpoints the SSR frontend calls. They must be cacheable, paginated, and free of surprises for a generated TypeScript client.

## Scope
- `GET /api/v1/collections`, `/collections/{slug}`, `/collections/{slug}/photos`, `/photos/{id}`, `/home`.
- DTOs as records in `Application`; `PhotoSummary` carries derivative URL pieces (base host + bucket path + sha) rather than pre-baked full URLs, so the host stays configuration.
- Pagination: `page` (1-based, default 1), `pageSize` (default 24, max 100); responses carry `items`, `page`, `pageSize`, `total`.
- 404 as `ProblemDetails` for unknown slug/id; 400 for out-of-range pagination.
- Ordering: collections by `Position`, photos by `Position`, featured by `FeaturedRank`.
- Neighbour ids (prev/next by `Position`) on `PhotoDetail`.
- No EF entity ever reaches the API surface.

## Done when
- Integration tests cover: empty database, a collection with 30 photos and `pageSize=10`, unknown slug, `pageSize=1000`, and stable ordering after a reorder.
- `contracts/openapi.json` reflects the endpoints and the generated client compiles.
- A collection query issues a bounded number of SQL statements (assert via `ToQueryString` in a test, not by eye).

## Comments

Implemented with a read-side `IPublicGalleryQueries` in Application and an EF projection implementation in Infrastructure — no entities cross the API boundary.

Decisions:

- **Derivative URLs are built by the client** from `sha256` + `availableWidths`; the API never bakes a hostname into a response, so the image host stays configuration.
- **`availableWidths` is computed per photo** (ladder ∩ ≤ original), so the frontend never requests a width that was not generated.
- **Photo detail resolves neighbours with two extra queries**, not a window function. At this scale the clarity is worth more than the round trip.
- **Unknown slug returns 404, an empty collection returns an empty page** — the distinction matters to the frontend's empty states.

Verified: nine API tests (paging validation, 404 shape, defaults) and five query tests against Postgres covering cover selection, pagination order, width caps, neighbours, EXIF, and soft delete.
