# 02 — Schema and migrations

Status: ready-for-agent
Blocked by: 01

## Context
Two tables, no joins. The interesting part is soft delete, contiguous ordering, and a unique slug that survives deletion.

## Scope
- `Collection` and `Photo` entities exactly as listed in the spec's Domain section; `exif` as `jsonb`, ids as ULID (store as `text` or `uuid`-compatible type, but keep the ULID string as the API identity).
- `Npgsql.EntityFrameworkCore.PostgreSQL`, one `DbContext`, configuration per entity (`IEntityTypeConfiguration`), no attributes on domain types.
- Indexes: unique `Collection.Slug` among non-deleted rows (partial index), `(Photo.CollectionId, Photo.Position)`, `(Photo.FeaturedRank)` where not null, `Photo.DeletedAt`.
- Check constraints: `Position >= 0`, `FeaturedRank >= 0`, `Width > 0`, `Height > 0`.
- Global query filters excluding soft-deleted rows, with an escape hatch for admin queries (`IgnoreQueryFilters`).
- Initial migration, plus a documented `dotnet ef` workflow in `backend/README.md`.

## Done when
- Migration applies to a Testcontainers Postgres instance in a test.
- Inserting two collections with the same slug fails; soft-deleting one and reusing the slug succeeds.
- Default queries never return soft-deleted rows; admin queries can.

## Comments

Implemented. Judgement calls worth recording:

- **ULIDs are generated in the domain** with the `Ulid` package (MIT) and stored as `varchar(26)`, not as a native Postgres type. Keeps ids portable and the API identity is the string itself.
- **A design-time factory** (`GalleryDbContextFactory`) reads `GALLERY_CONNECTION` and otherwise points at localhost, so `dotnet ef` never needs the API to boot.
- **`dotnet-ef` is a local tool** (`backend/dotnet-tools.json`), so CI runs `dotnet tool restore` and gets the pinned version.
- **The unique index on `(CollectionId, Position)` is deliberate** and means reordering must be two-phase — noted on ticket 06 before it becomes a runtime surprise.

Verified: four tests against Testcontainers Postgres (`postgres:18`), including the partial unique index freeing a slug after soft delete, query-filter invisibility, and a rejection of negative positions.
