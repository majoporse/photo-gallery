# 01 — Move the chart and merge platform values

Status: ready-for-agent
Blocked by: none

## Context
One install should produce the app plus the platform CRs. The existing values
already cover the app; the platform pieces live as fixed manifests with
namespace `photo-gallery` and hardcoded hosts.

## Scope
- `git mv deploy/app/photo-gallery deploy/photo-gallery`, delete `deploy/platform`.
- Add `platform.*` values (postgres, seaweedfs, certManager, imageIngress),
  the hierarchical `secrets.*` block, and make `config.storageEndpoint` an
  override for external storage.
- Keep resource names the old manifests used (`pg`, `seaweed`,
  `letsencrypt-prod`, `seaweed-s3-public`, `gallery-secrets`, `pg-bootstrap`) so
  existing installs can be adopted.
- Update the schema and validation for the new required values and conditional
  requirements.

## Done when
- `helm lint` passes with the placeholder values.
- `helm template` fails with a clear message when `secrets.app.admin.passwordHash`, the ACME
  email, or a required external endpoint is missing.

## Comments

Implemented 2026-10-29.

`deploy/app/photo-gallery` moved to `deploy/photo-gallery`; `deploy/platform`
is gone. Values now group `secrets.app`/`secrets.postgres`/`secrets.storage` and `platform.*`
(postgres, seaweedfs, certManager, imageIngress). Resource names used by the old
manifests (`pg`, `seaweed`, `letsencrypt-prod`, `seaweed-s3-public`,
`gallery-secrets`, `pg-bootstrap`, `img-tls`) are kept so adoption is possible.

The schema requires `secrets.app.admin.passwordHash` and accepts the new platform section;
`storageEndpoint` and `ingress.certManager.clusterIssuer` now default empty and
are required only when the matching platform piece is external/not created.
`validation.yaml` covers the conditional requirements and the 32-character
signing key minimum.

Verified: `helm lint` passes; `helm template` fails with
`required values not set: secrets.app.admin.passwordHash`,
`... platform.certManager.issuer.email`, and
`... platform.postgres.connectionString (with postgres.enabled=false)` when each
is omitted.

