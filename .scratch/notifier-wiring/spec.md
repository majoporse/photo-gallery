# Notifier wiring

Status: implemented. All six tickets are closed.

The admin upload stream moved out of the API into `Gallery.Notifier` (ADR 0010).
That split is correct, but three entry points were left pointing at the old
topology: both Compose variants, the Helm chart's browser-facing API URL, and the
committed OpenAPI contract. This effort makes local startup and cluster startup
match the new service, without changing the architecture.

Findings came from a review on 2026-10-29. Evidence: the API answers
`/api/v1/admin/uploads/events` with 404, the notifier answers 401 on :8081, and
the browser still opens the stream on `PUBLIC_API_URL`.

## Tickets

| # | Title | Severity |
| - | ----- | -------- |
| 01 | Point local development at the notifier's stream | blocker |
| 02 | Derive the browser's API URL from `ingress.apiHost` | config |
| 03 | Give the notifier a readiness signal | config |
| 04 | Keep the SSE endpoint in the committed contract | contract |
| 05 | Pin the notifier to one replica | guardrail |
| 06 | Correct the notifier docs and comments | docs |

## Non-goals

No queue redesign, no notifier replicas, no new proxy tier in Compose. Production
routing (exact-path ingress on the API host) stays as ADR 0010 describes.
