# 01 — Point local development at the notifier's stream

Status: ready-for-agent
Blocked by: none

## Context

`openUploadStream` builds its URL from `PUBLIC_API_URL`
(`frontend/app/api/admin.ts:307`). Compose sets that to `http://localhost:8080`
(`compose.yaml:171`), which is the API. After the notifier split the API returns
404 for `/api/v1/admin/uploads/events`; the notifier serves it on host port 8081.
`EventSource` therefore never opens, the snapshot fetch never runs, and upload
rows stay at "Queued" forever. CI's e2e job asserts "Added" and "Rejected"
(`frontend/e2e/admin.spec.ts:65-72`), so it fails too.

The notifier is already reachable at 8081 and its CORS policy trusts
`http://localhost:3000`; only the browser URL is wrong. Cookies ignore ports, so
the dev `pg_session` cookie reaches 8081 on the same site.

## Scope

- `frontend/app/lib/env.server.ts`: add `getEventsUrl()` — explicit
  `PUBLIC_EVENTS_URL`, else `http://localhost:8081` when the public API URL is a
  loopback address, else the public API URL (so Helm's ingress routing still
  applies unchanged).
- Expose it as `publicEventsUrl` in the root loader, `RootData`, and
  `window.__PUBLIC_EVENTS_URL__`; use it for `EventSource` only.
- Document `PUBLIC_EVENTS_URL` in `frontend/.env.example` and the root
  `.env.example`, and pass it through the web service in `compose.yaml`.
- Unit tests for the three resolution cases.

## Done when

- Fresh clone: `cp .env.example .env && docker compose up --build`, upload a tiny
  PNG in the admin UI, and the row reaches Rejected without any extra env var.
- Native loop (`compose.storage.yaml`, `dotnet run` × 3, `npm run dev`) behaves
  the same with no `frontend/.env`.
- `helm template` still renders an events URL on the public API host.

## Comments

Implemented 2026-10-29.

- `getEventsUrl()` resolves an explicit `PUBLIC_EVENTS_URL` first, then the
  local notifier port when the public API URL is loopback, then the public API
  URL. Production therefore needs no extra value: the ingress exact-path rule
  still applies.
- The root loader exposes `publicEventsUrl` and the document sets
  `window.__PUBLIC_EVENTS_URL__`; `openUploadStream` is the only consumer, so
  every other admin call stays on `PUBLIC_API_URL`.
- `PUBLIC_EVENTS_URL` is passed through the Compose web service and documented
  in both env examples, with a native default that needs no `frontend/.env`.
- Three unit tests cover local, deployed and explicit resolution.

Verified with the full Compose stack: SSR HTML carries
`window.__PUBLIC_EVENTS_URL__="http://localhost:8081"`, the notifier answers the
stream with 401 unauthenticated, and the existing e2e test "owner signs in,
creates a collection, and uploads with a per-file rejection" passes — it asserts
the terminal Added/Rejected labels that only arrive over SSE.
