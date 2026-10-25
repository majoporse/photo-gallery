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

## Comments

Chart built and verified statically: `helm lint` clean, `helm template` renders 7 objects with no `<no value>`, missing required values fail with `required values not set: …`, and `kubectl apply --dry-run=client` accepted the output.

Judgement calls worth knowing:

- **Release-based resource names** (`<release>-api`, `<release>-web`) so `API_INTERNAL_URL` stays readable.
- **`__required__` placeholders plus a validation template** instead of empty defaults: `helm lint` validates `values.yaml` against the schema, so empty required values would break the bare lint command. Validation is gated on `IsInstall`/`IsUpgrade`.
- **`proxy-body-size: 55m`** on the API ingress — ingress-nginx defaults to 1 MB and would reject uploads before the API's own 50 MB limit.
- **Web health contract is `GET /healthz`**, process-local, no API or database dependency. The frontend must implement it; it does not exist yet.
- **Anti-affinity defaults to `preferred`**, because `required` leaves a second replica Pending on single-node clusters.

Changed after review: images now come from the cluster's **private registry with basic auth** (not GHCR), so `image.pullSecrets` defaults to `[photo-gallery-registry]` and the runbook creates that secret before the first rollout.

Still unverified, and only provable on a real cluster: TLS issuance, image compatibility with UID 1000 and a read-only root filesystem, and the rolling-replacement assertion.
