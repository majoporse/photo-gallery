# 02 — Derive the browser's API URL from `ingress.apiHost`

Status: ready-for-agent
Blocked by: none

## Context

The web Deployment passes `API_INTERNAL_URL`, `PUBLIC_SITE_URL` and
`IMAGE_BASE_URL`, but not `PUBLIC_API_URL`. `getPublicApiUrl()` synthesises
`api.<siteHost>` when it is absent. That happens to match the runbook, but the
chart exposes `ingress.apiHost` as an independent value. An operator who sets it
to anything else gets a site whose browser-side admin calls (including the stream)
go to a host that does not exist, while probes and SSR stay green.

## Scope

- Add `web.publicApiUrl` to `values.yaml` and `values.schema.json`. Empty derives
  `https://<ingress.apiHost>`.
- Render `PUBLIC_API_URL` in `templates/web-deployment.yaml` from it.
- Document the value in the chart README.
- Keep `PUBLIC_EVENTS_URL` unset: it falls back to the API host, which the
  ingress routes to the notifier.

## Done when

- `helm template` with `--set ingress.apiHost=api.other.example` renders
  `PUBLIC_API_URL=https://api.other.example`.
- The default render still produces `https://api.<siteHost>`.

## Comments

Implemented 2026-10-29.

Added `web.publicApiUrl` to the values, schema and chart README, and render
`PUBLIC_API_URL` from `default (printf "https://%s" .Values.ingress.apiHost)`.
Verified: `--set ingress.apiHost=api.other.example` renders
`PUBLIC_API_URL="https://api.other.example"`; the runbook's
`api.$DOMAIN` value still renders `https://api.example.com`. `helm lint` passes.

