# 12 — CI pipeline: build, contract drift, images, deploy

Status: ready-for-agent
Blocked by: 01, 07, 11

## Context
Four jobs and one rule that matters: the committed OpenAPI document must match what the API actually serves, or the generated client silently rots. Regenerating the client is the dev loop; the drift check is the safety net, because a stale `openapi.json` regenerates into a stale client that still typechecks.

## Scope
- `.github/workflows/ci.yml` with jobs:
  - `api` — restore, build, unit and integration tests (Testcontainers Postgres + SeaweedFS), then export the spec and `git diff --exit-code contracts/openapi.json`.
  - `web` — `npm ci`, typecheck, lint, Vitest, `npm run build`.
  - `e2e` — `docker compose up`, wait for health, Playwright (ticket 13).
  - `release` — on a tag, build both images, log in to the cluster's private registry with `REGISTRY_HOST`/`REGISTRY_USER`/`REGISTRY_PASSWORD` secrets, and push with `sha` and semver tags.
- Deploy job on tag: apply secrets from GitHub Actions secrets (including the registry `imagePullSecret`), then run the operator sequence from `docs/deploy/runbook.md` in order, then `helm upgrade --install` with `image.api.repository` and `image.web.repository` pointed at the registry. Concurrency guard so two deploys cannot overlap.
- Caching: NuGet, npm, and Docker layer cache, because a five-minute pipeline is one you actually use.
- A pre-commit hook (documented, not enforced by CI) running the same drift check for the two-second feedback loop.

## Done when
- A deliberate DTO rename without re-exporting the spec fails `api` with a readable diff.
- A type error in the frontend fails `web`.
- Pushing a tag produces two images in GHCR and a deployed revision.
- Total runtime for a normal PR is under 10 minutes.

## Comments

Implemented as four jobs: `api` (build, Testcontainers tests, contract drift), `web` (lint, typecheck, Vitest, build), `release` (tag only, images to the private registry) and `deploy` (tag only, secrets then `helm upgrade`).

Decisions:

- **The SDK is pinned to `10.0.113`**, not `10.0.x`. The drift check compares generated OpenAPI byte for byte; a patch-level generator change would fail unrelated PRs.
- **Deploy applies both secrets idempotently** (`create --dry-run=client -o yaml | kubectl apply -f -`), so rotating the admin hash or registry password is a normal deploy.
- **The connection string is assembled at deploy time** from CloudNativePG's `pg-app` secret, rather than duplicated into GitHub, so the database password has exactly one source of truth.
- **The e2e job is deliberately absent**: Playwright tests do not exist until ticket 13, and a job that is red by construction trains people to ignore CI. It joins this workflow in ticket 13.
- Required repository secrets are listed at the bottom of the workflow file.

Verified locally against the exact commands the jobs run: Release build warning-free, 49 tests passing with `--no-build -c Release`, `git diff --exit-code -- contracts/openapi.json` clean, and the workflow parses as YAML. **Not verified**: an actual GitHub Actions run, image push to the registry, and the deploy job against a live cluster.
