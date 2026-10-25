# 11 — Helm chart and the three ingresses

Status: ready-for-agent
Blocked by: 01

## Context
Platform manifests already exist under `deploy/platform/`. This ticket is the application chart plus the public edge, including the access rules that keep originals private.

## Scope
- `deploy/app/photo-gallery/` with `Chart.yaml`, `values.yaml`, `templates/` for: api Deployment/Service, web Deployment/Service, one Ingress per host (site, `api.`, `img.`), ConfigMap for non-secret config, and secret references by name only (never inline values).
- Probes: `/api/v1/health` for the API; the SSR server's own health route for the web pod; readiness must not depend on the database in a way that flaps the rollout.
- Resource requests/limits, `securityContext` (non-root, read-only root filesystem, dropped capabilities), and a pod anti-affinity rule so two replicas do not share a node.
- The `img.` ingress stays platform-owned (`deploy/platform/seaweedfs/ingress.yaml`); the chart only emits the certificate annotation and documents the hostname contract. Do not duplicate it.
- `values.schema.json` for the handful of required values, so a missing image tag fails at template time.
- `helm lint` and `helm template` pass in CI.

## Done when
- `helm template` with a representative values file renders every object with no `<no value>` in it.
- Deployed to a local cluster (kind/k3s) with the platform manifests applied, the site answers over TLS and the admin can log in.
- `GET https://img.<host>/?Action=ListUsers` and `GET https://img.<host>/originals/` both return 404; `GET /photos/<key>` returns the image.
- Deleting a web pod does not produce a 5xx during the replacement.
