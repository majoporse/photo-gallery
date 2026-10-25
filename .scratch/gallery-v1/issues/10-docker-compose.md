# 10 — Local development with Docker Compose

Status: ready-for-agent
Blocked by: 01

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
