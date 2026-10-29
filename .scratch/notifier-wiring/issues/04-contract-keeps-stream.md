# 04 — Keep the SSE endpoint in the committed contract

Status: ready-for-agent
Blocked by: none

## Context

Commit 967615b moved the stream into the notifier and deleted
`/api/v1/admin/uploads/events` from `contracts/openapi.json`. The route is still
live (via the API Ingress), and the frontend still calls it by hand. The contract
drift check only generates from the API project, so it cannot see the notifier and
the regression passed CI. Consumers of the published document lost the endpoint
without a trace.

## Scope

- Add an OpenAPI document transformer in `Gallery.Api` that declares the stream
  path, with a comment explaining that the ingress, not the API process, is what
  routes it. Keep the previous shape: tag `AdminUploads`, operationId
  `AdminUploadEvents`, GET, 200 `text/event-stream`.
- Regenerate `contracts/openapi.json` through the normal build.
- Record the deliberate proxy hop in ADR 0010.

## Done when

- The committed document contains the path again with the same shape it had
  before 967615b.
- `dotnet build` leaves `git diff -- contracts/openapi.json` clean.
- Orval still generates, and `npm run typecheck` and `npm run lint` pass.

## Comments

Implemented 2026-10-29.

`Configuration/NotifierStreamDocument.cs` adds the path through an
`AddDocumentTransformer` on `AddOpenApi("v1")`. The generated diff restores
exactly the 16 lines removed in 967615b: tag `AdminUploads`, operationId
`AdminUploadEvents`, GET, 200 `text/event-stream`. Orval regenerates
`getAdminUploadEvents` (unused, as before) and the frontend typecheck, lint and
unit tests pass. ADR 0010 records why the API document declares a path the
process does not route.

