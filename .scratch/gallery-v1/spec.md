# Gallery v1

Status: implemented. All v1 tickets are closed except the deferred nightly backup (ticket 14).

A single-owner photography archive presented as a public web gallery. The owner publishes; everyone else only looks.

## Goal

Upload a photograph, see it on a public, indexable page within seconds, without running any deploy. Browse collections without fighting the UI. Everything the owner needs to undo a mistake lives in the database, not in S3.

## Non-goals

No public accounts, comments, likes, search, tags, or multi-user access. No e-commerce or print sales. No mobile app. No backup automation in v1 (accepted risk). No S3 versioning or object lock.

## Domain

See `GLOSSARY.md` for vocabulary. Two aggregates and one join-free relationship.

**Collection** — `id` (ULID), `slug` (unique, immutable), `name`, `description?`, `position`, `createdAt`, `updatedAt`, `deletedAt?`.

**Photo** — `id` (ULID), `collectionId`, `position`, `title?`, `caption?`, `altText?`, `takenAt?`, `width`, `height`, `originalKey`, `originalSha256`, `dominantColor`, `exif` (jsonb), `featuredRank?`, `createdAt`, `updatedAt`, `deletedAt?`.

Invariants:

- A Photo belongs to exactly one Collection. Moving it is a `PATCH`, not a second membership.
- `position` is contiguous and zero-based within a Collection; the reorder endpoint rewrites the whole range.
- `slug` never changes — photo and collection URLs are permanent, because they are shared and indexed.
- Deleting a Collection soft-deletes its Photos. Nothing is ever hard-deleted by the API.
- `featuredRank` is a global ordering for the home page, independent of `position`.
- Required fields are minimal: a Collection needs a `name`; a Photo needs a file. Everything else is optional, including `altText`.

## API

`/api/v1`. Lists paginate with `page` (1-based) and `pageSize` (default 24, max 100). Errors are `ProblemDetails` (RFC 9457). Timestamps are ISO 8601 UTC. The OpenAPI document is generated at build time by `Microsoft.AspNetCore.OpenApi`, committed to `contracts/openapi.json`, and the frontend client is generated from it by Orval.

### Public

| Method | Path | Returns |
|---|---|---|
| GET | `/collections` | `{ items: CollectionSummary[], page, pageSize, total }` |
| GET | `/collections/{slug}` | `CollectionDetail` (summary + `photoCount` + `cover`) |
| GET | `/collections/{slug}/photos` | `{ items: PhotoSummary[], page, pageSize, total }` |
| GET | `/photos/{id}` | `PhotoDetail` (includes full EXIF and derivative URLs) |
| GET | `/home` | `{ featured: PhotoSummary[], collections: CollectionSummary[] }` |
| GET | `/intro?count=10` | random `PhotoSummary[]` for the landing sequence, max 24 |
| GET | `/health` | liveness/readiness for the ingress |

`PhotoSummary` carries `id`, `width`, `height`, `dominantColor`, `altText`, `takenAt`, and the derivative URL template; `PhotoDetail` adds `title`, `caption`, `exif`, collection reference, and neighbour ids for prev/next.

### Admin

All require the session cookie; all mutations additionally require `X-Requested-With: photo-gallery` and an allowlisted `Origin`.

| Method | Path | Purpose |
|---|---|---|
| POST | `/admin/session` | `{ username, password }` → 204 + `Set-Cookie` |
| DELETE | `/admin/session` | clears the cookie |
| GET | `/admin/collections` | all collections incl. soft-deleted, unpaginated |
| POST | `/admin/collections` | `{ name, slug, description? }` |
| PATCH | `/admin/collections/{id}` | name, description |
| DELETE | `/admin/collections/{id}` | soft delete + cascade |
| PUT | `/admin/collections/order` | `{ ids: string[] }` |
| POST | `/admin/collections/{id}/photos` | `multipart/form-data`, repeated `files` → `{ created: PhotoSummary[], failed: { fileName, reason }[] }` |
| PATCH | `/admin/photos/{id}` | title, caption, altText, takenAt, featuredRank, collectionId |
| DELETE | `/admin/photos/{id}` | soft delete |
| PUT | `/admin/collections/{id}/photos/order` | `{ ids: string[] }` |

Upload rejects anything over 50 MB, anything whose magic bytes disagree with its extension, and anything smaller than 640 px on the long edge. Partial failure returns 201 with the `failed` array — one bad file does not lose the batch.

### Auth

One Admin, seeded from configuration (`Admin__Username`, `Admin__PasswordHash`, PBKDF2 or Argon2). Login issues an HS256 JWT (12 h, no refresh) in a cookie: `HttpOnly; Secure; SameSite=None; Path=/`, name `pg_session`. Logout clears it; rotating `Auth__SigningKey` invalidates every session. Login is rate-limited to 5 attempts/minute per IP. No registration, no password reset, no user table.

## Storage

**Two buckets**, because SeaweedFS grants anonymous access per bucket and never per prefix — one bucket would make originals world-readable the moment derivatives were published.

```
photos/derivatives/{sha256}/{width}w.{avif|webp|jpg}   anonymous Read+List, internet-facing
originals/{photoId}/{sanitised-filename}               private, no anonymous access
```

Derivative keys are content-addressed, so identical uploads share bytes and a re-run is idempotent. Widths: **320, 640, 1280, 2048**, in AVIF, WebP and JPEG (JPEG is the `img src` fallback). Widths above the original are skipped rather than upscaled, so the ladder is always the largest subset of four. Encoding is Magick.NET — SkiaSharp's `Avif` format exists but its encoder returns nothing. Objects are written with `Cache-Control: public, max-age=31536000, immutable`, and SeaweedFS returns that header on GET.

Public URL shape: `https://img.<domain>/photos/derivatives/{sha}/{width}w.{ext}`. The ingress for that host proxies **only** `/photos` to the S3 gateway on 8333; every other path — including the IAM API, which shares that port at the root path — is unanswered. See `deploy/platform/seaweedfs/ingress.yaml`.

Anonymous access is granted with `s3.bucket.access -name photos -user anonymous -access Read,List`. The `List` grant is what permits nothing more than walking the derivative keys; the bucket holds no other objects.

Upload pipeline, synchronous within the request: sniff magic bytes → compute SHA-256 → stream the original to S3 untouched → read into memory at a bounded size → write the ladder in parallel → extract EXIF and dominant colour → insert the row. **Every image format the detector recognises is accepted** — JPEG, PNG, WebP, GIF, BMP, TIFF, AVIF, HEIC/HEIF — because the archive should not refuse a photograph over its container. The extension a file claims is irrelevant: content wins, and a TIFF named `.jpg` is stored as `image/tiff`. The guards that remain are about size, not format: 50 MB and 120 megapixels, the latter checked after decode because a compressed file can expand enormously. Magick.NET (Apache-2.0) does decode, auto-orient, resize, encoding and EXIF extraction; SkiaSharp cannot encode AVIF, and ImageSharp is excluded for licensing reasons. Derivatives are stripped of metadata.

## Frontend

React 19, Vite 8, TypeScript 7 for `tsc` (TS 6 aliased for typescript-eslint until it supports 7), React Router 8 framework mode with `ssr: true`, TanStack Query 5, shadcn/ui on Tailwind 4, Embla Carousel, yet-another-react-lightbox, Motion 14.

| Route | Rendering | Notes |
|---|---|---|
| `/` | server `loader` | scroll-driven intro (ten random photos, name reveal), then featured carousel and collection links |
| `/collections` | server `loader` | grid |
| `/collections/:slug` | server `loader` | paginated grid, lightbox overlay |
| `/photos/:id` | server `loader` | deep link, `og:image` = 1280 derivative, prev/next |
| `/about` | static | copy lives in the route module |
| `/admin/*` | `clientLoader` only | login, collections, upload, reorder; `noindex` |

Client-only by nature and mount-gated: lightbox, drag-to-reorder (dnd-kit sortable), theme toggle, upload progress.

Data: public queries hydrate through `HydrationBoundary` with `staleTime` of 60 s to avoid an immediate refetch flash; admin queries use their own `QueryClient` with `staleTime: 0`.

Theme: three palettes selected by `data-theme` on `<html>` — `paper` (warm off-white, near-black ink), `ink` (near-black, light text), `grey` (mid-dark neutral, the Lightroom-chrome range, `#2b2b2b`). Radius is `0` everywhere; hairline 1px rules instead of cards or shadows. The choice persists in a `theme` cookie and is applied by a tiny inline script in `<head>` to avoid a flash. Owner details — name, biography, email, social and repository links — live in `frontend/app/content/site.ts`; the intro and footer read from the same file.

## Deployment

Local: `compose.yaml` with `postgres:18`, SeaweedFS (`weed mini`, S3 on 8333), a one-shot `seaweed-init` that creates both buckets and grants anonymous read on `photos`, a one-shot `migrate` that runs the API image with the `migrate` argument, the API, and the Node SSR server. No `version:` key; project name `photo-gallery`. A `seed` profile uploads two generated photographs through the admin API so the site has content without a manual upload.

Production: one cluster, three hostnames — the site, `api.`, and the image host — with cert-manager issuing all certificates. Workloads: `api` (2 replicas), `web` (2 replicas), a CloudNativePG `Cluster` (1 instance), and a SeaweedFS `Seaweed` (1 master with persistence, 1 volume on a PVC, 1 filer on leveldb2, 1 S3 gateway). Operators are installed as separate Helm releases, sequenced before the app; `Chart.yaml` cannot express that dependency. See `docs/deploy/runbook.md`.

CI (GitHub Actions): `api` builds and tests then exports the spec and fails on drift; `web` typechecks, lints and runs Vitest; `e2e` brings up Compose and runs Playwright over three flows; on a tag, images are pushed to the cluster's **private registry** (basic auth, credentials in CI) and a deploy job runs `helm upgrade --install`. Pods pull with the `photo-gallery-registry` imagePullSecret, which must exist in the namespace before the first rollout.

## Risks and deferred work

- **No backups.** The NAS copy of originals is the archive of record, so only Postgres metadata is irreplaceable — and it is currently unprotected. Highest-value follow-up: a nightly `pg_dump` CronJob.
- **Full EXIF, public, indexed** — see ADR 0002. Accepted, with consequences.
- **TypeScript 7 / typescript-eslint gap** — lint is pinned to TS 6 until upstream catches up; the two-`typescript` alias is a temporary wart.
- **Magick.NET is a large native dependency**, chosen because it is the only Apache-2.0 option that encodes AVIF. Dropping AVIF would let the ladder move back to SkiaSharp.
- **The image host is the object store.** Public reads bypass the API entirely, so the only thing separating derivatives from originals is the two-bucket split plus the `/photos` path rule on the ingress. CI should assert that `GET /originals/...` and `GET /?Action=...` on the image host return 404, so a future ingress edit cannot silently re-expose them.
- Pagination exists on public endpoints but admin lists return everything; fine at this scale, revisit past ~2k photos.

## Open questions

1. **Fonts and palette.** Deferred by decision; the frontend ships with placeholder tokens.
