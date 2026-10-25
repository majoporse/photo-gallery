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
