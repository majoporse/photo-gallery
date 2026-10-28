# 15 — API reference: browsable OpenAPI docs on the api host and the site

Status: ready-for-agent
Blocked by: none

## Context
The contract already exists in three places: `Microsoft.AspNetCore.OpenApi` generates it (`backend/src/Gallery.Api/Program.cs:30`), `contracts/openapi.json` is committed and drift-checked in CI, and Orval builds `frontend/app/api/generated/` from it. What is missing is a page a human can read.

Today the document is only reachable at `GET /openapi/v1.json`, and only when `IsDevelopment()` is true (`Program.cs:147-150`). No UI is served, and the site has no route for it. Reading the API therefore means reading raw JSON or the prose table in `spec.md`.

Decision recorded here so the agent does not have to guess: the document is already public in the repository, so publishing a rendered page discloses nothing new. Serve it publicly in every environment rather than behind the dev flag.

Package choices, checked against the registries on 2026-10-26:
- `Scalar.AspNetCore` 2.18.3 (NuGet) — `app.MapScalarApiReference()`, reuses the existing `AddOpenApi("v1")`.
- `Swashbuckle.AspNetCore` 10.3.1 (NuGet) — the older Swagger-UI route; it would add a second generator next to the one we already use. Do not add both.
- `@scalar/api-reference-react` 0.9.83 (npm) or `redoc` 3.0.1 (npm) for the site page. Scalar keeps one vocabulary across both hosts; Redoc is the heavier three-panel fallback.

## Scope

Backend
- Serve the document and a UI in every environment, at `api.<domain>/docs` and `api.<domain>/openapi/v1.json`. Keep the existing `AddOpenApi("v1")`; do not introduce a second generator or a hand-maintained spec.
- Check the `api.` ingress in `deploy/` — if it routes only `/api`, extend it for `/docs` and the document path, and confirm the `x-forwarded` scheme does not make the UI request the JSON over `http://` from an `https://` page.
- The docs page must be reachable without a session cookie and must return 200 from a freshly rolled pod.
- Do not break the build-time export path (`--applicationName` in `Program.cs`): the export runs in an environment with no secrets, and the docs mapping must not require any.

Frontend
- New `/api-docs` route rendering the same document, client-only and lazily imported so the reference bundle stays out of the public routes' chunks.
- Source the JSON from the committed `contracts/openapi.json` by copying it into `public/` during the build, so the bytes the browser fetches are the bytes CI drift-checks. Do not hand-copy it into the repo.
- `noindex` on the route, and no sitemap entry — it belongs to the developer surface, not the gallery.
- Link it once, from the footer or from `frontend/app/content/site.ts`; it does not belong in the main nav.

## Done when
- `GET /docs` returns 200 with the document rendered on the deployed api host, not only in `Development`.
- `GET /api-docs` on the site renders the reference, fetches its JSON with a 200, carries `noindex`, and is absent from `sitemap.xml`.
- Both pages list the public and admin endpoints and expand `PhotoSummary` and `CollectionDetail`.
- `contracts/openapi.json` is unchanged by this ticket, unless the change is a deliberate contract edit with the Orval client regenerated in the same commit.
- Backend tests and the frontend typecheck/lint/Vitest run stay green; the `/api-docs` chunk is not present in the `/` route's bundle.

## Comments

Implemented with `Scalar.AspNetCore` 2.18.3 (API) and `@scalar/api-reference-react` 0.9.83 (site), both exact and verified against their registries.

Backend:

- `AddOpenApi("v1")` is unchanged. `MapOpenApi()` and `MapScalarApiReference("/docs")` now run in every environment, including the no-secrets build-time document export.
- Scalar resolves a relative source against the docs page, not the origin, so the source URL is absolute (`scheme://host/openapi/{documentName}.json`). `UseForwardedHeaders` now trusts `X-Forwarded-Proto`/`-Host`/`-For` from the ingress, making an https page fetch https JSON instead of being blocked as mixed content.
- `GET /docs` returns 200 directly. Scalar.AspNetCore redirects a slashless prefix to `/docs/` because its embedded assets are relative; a small path rewrite plus `<base href="/docs/">` keeps the public URL and resolves the assets. `/docs/`, `/docs/scalar.js`, `/docs/scalar.aspnetcore.js`, `/docs/favicon.svg` and `/openapi/v1.json` all return 200, without a cookie, in `Production`, with no required configuration present.
- The `api.` ingress already routes `/` to the API, so `/docs` needed no manifest change.
- `DocsTests` covers the 200s, the base tag and the forwarded scheme.

Frontend:

- `/api-docs` is client-only (`useMounted`) and imports the reference through `React.lazy`, so Scalar lands in `api-reference-*.js`; neither `home` nor `root` references that chunk.
- `scripts/copy-openapi.mjs` copies `contracts/openapi.json` to `public/openapi.json` from `prebuild` and `dev`; the generated copy is gitignored. The page fetches `/openapi.json` and gets 200.
- The route meta is `noindex, nofollow`, the sitemap has no entry, and the footer carries the single link.
- An e2e edge test asserts the document fetch, the noindex meta, sitemap absence and that the reference renders.

Verified: backend build plus 59 tests green with `contracts/openapi.json` unchanged; frontend typecheck, lint, 49 Vitest tests and the production build green; the production build serves `/api-docs`, fetches `/openapi.json` 200, renders the public and admin operations, and lists `PhotoSummary` under Models. The new Playwright edge tests pass against the production build.

Two deviations:

- **`CollectionDetail` does not exist in the committed contract.** `GET /collections/{slug}` returns `CollectionSummary`, which already carries `photoCount` and `cover`. The contract was left unchanged as the ticket requires; the reference shows `CollectionSummary`, `PhotoDetail` and `PhotoSummary`, and all admin operations.
- **`/docs` needed the rewrite/base workaround above.** The package's own behaviour is a 302 from `/docs` to `/docs/`; serving the document at the slashless URL required the base tag so the relative assets still resolve.

Not run: the full Compose e2e suite. Only the new edge tests were run, against the production web build.
