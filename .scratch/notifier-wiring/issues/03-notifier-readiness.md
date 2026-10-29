# 03 — Give the notifier a readiness signal

Status: ready-for-agent
Blocked by: none

## Context

`Gallery.Notifier` deliberately skips Wolverine when `ConnectionStrings__Gallery`
is absent and substitutes a placeholder signing key when `Auth__SigningKey` is
absent. Both keep the build and tests constructible, but a deployed pod with
either gap is green: `/api/v1/health` returns 200 while no event can ever be
delivered. Verified locally: the notifier boots with no configuration at all and
answers 200. In Helm a missing secret key therefore looks healthy.

## Scope

- Compute missing notifier settings (`Auth:SigningKey`,
  `ConnectionStrings:Gallery`) once at startup, like `RequiredSettings` does for
  the API, and log them at Error level.
- Add `GET /api/v1/health/ready` returning 200 `{"status":"ready",...}` or 503
  `{"status":"degraded","missingConfiguration":[...]}`. Keep `/api/v1/health` as
  liveness so a misconfigured pod is not restarted in a loop.
- Point the Helm notifier readiness probe and the Compose healthcheck at the
  readiness endpoint.
- Wait for it in CI's e2e stack gate.

## Done when

- No config: liveness 200, readiness 503 naming both keys.
- Compose/Helm config: readiness 200.
- `helm template` shows the readiness probe on `/api/v1/health/ready`.

## Comments

Implemented 2026-10-29.

`Program.cs` now accumulates the missing settings once, logs them at Error level,
and maps `/api/v1/health/ready` to a 200/503 `HealthResponse` shaped like the
API's. `/api/v1/health` stays unconditional liveness. The Helm readiness probe,
the Compose healthcheck and the CI e2e stack gate all use the readiness path.

Verified: running the notifier with no launch profile answers liveness 200 and
readiness 503 with
`{"status":"degraded","missingConfiguration":["Auth:SigningKey","ConnectionStrings:Gallery"]}`,
and logs the same keys. With the Compose environment it reports healthy and the
stream answers 401. `helm template` puts the readiness probe on
`/api/v1/health/ready`.

