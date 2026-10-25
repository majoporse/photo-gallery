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
