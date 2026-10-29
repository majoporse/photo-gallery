# 05 — Update CI, runbook, READMEs; remove the split manifests

Status: ready-for-agent
Blocked by: 01–04

## Context
The chart path, the deploy workflow and the operator runbook all assume the old
split. The workflow currently builds `gallery-secrets` from `pg-app`, which the
chart now owns.

## Scope
- CI deploy job: new chart path, remove the application-secrets step, pass the
  admin hash and ACME email, add a one-time adoption step for resources created
  by the old manifests.
- Runbook: operators only in steps 1–4; single chart install; verify CNPG,
  Seaweed, buckets/IAM, stream and image host; no `deploy/platform` references.
- Chart README: what the chart now owns, prerequisites, generated secrets,
  adoption, rotation; remove the "does not create" section.
- Root README layout/deploy row.

## Done when
- `grep -r deploy/platform` returns only the migration note, if any.
- The runbook install command is the one CI uses.

## Comments

Implemented 2026-10-29.

- CI deploy job: chart path `deploy/photo-gallery`, the application-secrets step
  is gone, `secrets.app.admin.passwordHash`/`secrets.app.admin.username`/`platform.certManager.issuer.email`
  are passed in, and an idempotent adoption step labels/annotates the resources
  created by the old split manifests. Required repo secrets are now
  `ADMIN_PASSWORD_HASH`, `ADMIN_USERNAME`, `ACME_EMAIL` and the hosts; the old
  storage/signing keys are no longer read.
- Runbook: operator sections keep their install commands; all `kubectl apply`
  of CRs, the bucket shell transcript and the image-ingress apply are removed.
  Steps 5–7 are pull secret + admin hash, the single Helm install, and verify.
- Chart README rewritten around one install, generated secrets, platform values,
  the adoption recipe, rotation and the web/image health contracts.
- Root README deploy/layout rows updated.

Verified: the only `deploy/platform` mention left is the migration comment in
the chart README; `ci.yml` and `values.yaml` parse as YAML; the runbook install
command matches the README.

