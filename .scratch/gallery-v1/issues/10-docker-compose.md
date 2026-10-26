# 10 — Local development with Docker Compose

Status: ready-for-agent
Blocked by: 01, 07

## Context
One command from a clean checkout to a working site, using the same storage engine as production so presigned URLs and cache headers behave identically.

## Scope
- `compose.yaml` at the repo root: `name: photo-gallery`, no `version:` key.
- Services: `postgres:18`, SeaweedFS (`weed mini`, S3 on 8333, filer UI exposed for inspection), `api`, `web`.
- A one-shot bootstrap service that creates the `photos` and `originals` buckets and the anonymous read grant, then exits; dependent services wait for it.
- Healthchecks on Postgres and SeaweedFS; `api` and `web` depend on healthy services.
- Optional `seed` profile that loads a handful of sample photographs so the frontend is developable without the owner's library.
- `.env.example` with the non-secret configuration and an obviously fake admin credential.

## Done when
- `docker compose up --build` from a clean state serves the site locally with no manual steps.
- Uploading through the admin works and the image renders from the local S3 endpoint.
- `docker compose down -v && docker compose up` reproduces from scratch.

## Comments

Verified from a clean volume: `down -v`, `up`, then `--profile seed` — Postgres, the migration one-shot, SeaweedFS, the bucket bootstrap, the API and the SSR server all come up healthy with no manual commands. The seed uploads two generated 1600×1000 JPEGs, the ladder produces 320/640/1280 in three formats, `GET /photos/derivatives/<sha>/640w.avif` returns `200 image/avif`, `GET /originals/` returns `403`, and the collection page server-renders its `<h1>`.

Four things that only surfaced by running it:

- **PostgreSQL 18 moved PGDATA.** Mounting `pgdata:/var/lib/postgresql/data` makes the entrypoint refuse to start; the volume now mounts `/var/lib/postgresql`.
- **Alpine splits ImageMagick coders.** Without `imagemagick-jpeg` the seed's `magick` has no JPEG encoder and fails with "no encode delegate".
- **`s3.configure` is a dry run without `-apply`.** It prints the change it would make and looks successful, while every upload later fails with "the access key ID you provided does not exist in our records". Fixed in Compose and in the runbook.
- **Compose interpolates `$` twice.** A PBKDF2 hash in `.env` loses its segments unless single-quoted, and a `$VAR` inside `command:` is substituted by Compose rather than the container shell unless escaped as `$$`.

Migrations are a one-shot `migrate` service using the API image with the `migrate` argument — the same entry point the chart's hook Job uses, so dev and production do not diverge.

**Changed later**: the separate Alpine seed container (curl + jq + ImageMagick) was replaced by a seeder inside the API. `migrate` now applies the schema and, when `Seed:Enabled` is true, downloads ten photographs from Lorem Picsum, runs them through the normal upload pipeline and features the first. Development profiles opt in; production never does. That removed a whole image, a shell script and its failure modes (Alpine's split ImageMagick coders, `jq` availability) from the compose stack.
