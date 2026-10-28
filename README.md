# Photo Gallery

A single-owner photography archive, published as a public gallery. The owner uploads; everyone else looks. No reader accounts, no comments, no commerce.

## Stack

| | |
|---|---|
| API | ASP.NET Core 10, MVC controllers, Clean Architecture (Domain / Application / Infrastructure / Api) |
| Worker | ASP.NET Core 10 host consuming a Postgres-backed queue (Wolverine), rendering the derivative ladder |
| Data | PostgreSQL 18 via EF Core; originals and derivatives in S3-compatible storage |
| Images | Magick.NET ladder: 320/640/1280/2048 in AVIF, WebP and JPEG |
| Web | React 19, React Router 8 with SSR, TanStack Query, Tailwind 4 + shadcn, Embla, Motion |
| Deploy | Docker Compose locally, Helm on Kubernetes with CloudNativePG, SeaweedFS and cert-manager |

## Run it locally

```bash
cp .env.example .env
docker compose up --build
```

On a fresh database the `migrate` one-shot also seeds ten photographs from
Lorem Picsum (`Seed__Enabled`), so the site has something to show immediately.

Site on <http://localhost:3000>, admin on `/admin` (`admin` / `dev-password`), API on <http://localhost:8080>, images on <http://localhost:8333>, filer UI on <http://localhost:8888>.

### Storage in containers, code on your machine

Faster feedback than rebuilding images:

```bash
docker compose -f compose.storage.yaml up -d      # Postgres, SeaweedFS, buckets, S3 identity
dotnet run --project backend/src/hosts/Gallery.Api -- migrate
dotnet run --project backend/src/hosts/Gallery.Api      # http://localhost:8080
dotnet run --project backend/src/hosts/Gallery.Worker   # renders uploads; optional
cd frontend && npm run dev                        # http://localhost:3000
```

No environment variables are needed. The API's development profile
(`backend/src/hosts/Gallery.Api/Properties/launchSettings.json`) already points at
those local ports with development credentials; the web app falls back to the
same defaults and reads an optional `frontend/.env` (see `frontend/.env.example`).

`dotnet run -- migrate` seeds ten photographs from Lorem Picsum on a fresh
database, because the development launch profile sets `Seed__Enabled=true`.
Production leave it unset, so a migrate can never invent content. Seed state is
idempotent: if the `sample` collection exists, the seeder does nothing.

## Layout

```
backend/     API, tests, Dockerfile
frontend/    SSR app, Dockerfile
contracts/   generated openapi.json — committed, drift-checked in CI
deploy/      Helm chart, platform manifests, Compose seed
docs/        ADRs, deployment runbook, agent conventions
.scratch/    the spec and tickets this was built from
```

## Tests

```bash
dotnet test backend/Gallery.slnx    # needs Docker: real Postgres and SeaweedFS
cd frontend && npm test             # unit tests
cd frontend && npm run test:e2e     # Playwright, needs `docker compose up`
```

## Read this before changing the design

- `GLOSSARY.md` — the vocabulary. A photo belongs to exactly one **Collection**; "gallery" means the site.
- `docs/adr/` — four decisions that look surprising without context, including why the site publishes full EXIF and why MinIO is not used.
- `.scratch/gallery-v1/spec.md` — the contract, endpoints and storage layout.
- `docs/deploy/runbook.md` — operator installation, in order, with commands.
