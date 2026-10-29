# 06 — Verify renders, app-only mode and the adoption path

Status: ready-for-agent
Blocked by: 01–05

## Context
No Kubernetes cluster is available locally, so the chart must be verified by
rendering. The old live install path matters because CI deploys into an existing
cluster.

## Scope
- `helm lint` and `helm template` with the full required values.
- Render with `platform.postgres.enabled=false` + connection string, and
  `platform.seaweedfs.enabled=false` + storage endpoint, and confirm no
  platform CRs.
- Confirm the notifier replica constraint still fails.
- Check that resource names still match the old manifests for adoption.
- Document the exact label/annotate commands and any remaining gaps.

## Done when
- Every render in the ticket is recorded in the comments with its result.
- The adoption command list covers CNPG, Seaweed, ClusterIssuer, image ingress,
  `gallery-secrets` and `pg-bootstrap`.

## Comments

Implemented and verified on 2026-10-29. No Kubernetes cluster is available in
this environment, so verification is by rendering (plus YAML parsing), not
server-side apply.

Results:

- `helm lint deploy/photo-gallery` passes with the placeholder values.
- Full `helm template` (release `gallery`, namespace `photo-gallery`, tags, hosts,
  ACME email and a PBKDF2 hash) renders 23 resources: 4 Deployments, 3 Services,
  3 Ingresses, the ConfigMap, 2 chart Secrets, the CNPG Cluster, Seaweed, 2
  Buckets, S3Identity/S3Policy/S3PolicyBinding/S3Credentials, ClusterIssuer and
  the migrate Job.
- App-only render
  (`postgres.enabled=false` + connection string, `seaweedfs.enabled=false` +
  storage endpoint, `issuer.create=false` + existing issuer) removes every
  platform CR, the image ingress and the DB wait containers.
- Custom `platform.postgres.name=db`, `platform.seaweedfs.name=weed`,
  `storagePublicBucket=derivs`, `storagePrivateBucket=raws` propagate to the DSN,
  `Storage__Endpoint`, bucket CRs, image-ingress paths, S3 policy resources and
  the app env.
- `notifier.replicas=2` fails schema validation.
- Missing `secrets.app.admin.passwordHash`, ACME email, or a required external endpoint fails
  with the named key.

Adoption: label/annotate `cluster.postgresql.cnpg.io/pg`,
`seaweed.seaweedfs.com/seaweed`, `ingress/seaweed-s3-public`,
`secret/gallery-secrets`, `secret/pg-bootstrap`, and cluster-scoped
`clusterissuer/letsencrypt-prod` with
`app.kubernetes.io/managed-by=Helm` plus the release-name/namespace annotations.
Documented in the chart README and automated idempotently in CI.

Known caveats, documented:

- The old `Seaweed` master/filer StatefulSets need one-time
  `kubectl delete statefulset ... --cascade=orphan` to pick up the new claim
  templates (persistence was previously not actually enabled).
- Generated values are reused via `lookup`; that path needs a cluster to test.
- The old `gallery` IAM user/keys remain in SeaweedFS but are unused after the
  switch to `gallery-api`/`gallery-s3`.

