# 02 — Template Postgres, SeaweedFS, buckets/IAM, issuer and image ingress

Status: ready-for-agent
Blocked by: 01

## Context
The split manifests are fixed to one namespace and host, and buckets/IAM are
created with `weed shell` by hand. Operator chart 0.1.44 ships `Bucket`
(with `anonymousRead`), `S3Identity` and `S3Credentials`, so all of it can be
declared.

## Scope
- `templates/postgres/cluster.yaml`: CNPG `Cluster`, bootstrap Secret name,
  instances/storage from values.
- `templates/seaweedfs/cluster.yaml`: the existing `Seaweed` spec, value-driven.
- `templates/seaweedfs/storage.yaml`: both `Bucket` CRs with `adoptExisting`,
  `Retain`, `anonymousRead` on the public bucket, `access` for the API identity;
  `S3Identity` + `S3Credentials` writing to `gallery-s3`.
- `templates/cert-manager/cluster-issuer.yaml`: `ClusterIssuer`, optional, name/solver/email
  from values.
- `templates/seaweedfs/ingress.yaml`: `/photos` and `/originals` on the image host,
  nginx body size for direct 50 MB PUTs (currently missing).
- All resources in the release namespace, no hardcoded `photo-gallery`.

## Done when
- A rendered install contains every CR with the values spelled out.
- `platform.seaweedfs.enabled=false` emits no Seaweed/bucket/IAM/image ingress.
- The image ingress carries `proxy-body-size`.

## Comments

Implemented 2026-10-29.

New templates: `postgres-cluster.yaml`, `seaweed.yaml`, `seaweed-storage.yaml`,
`cluster-issuer.yaml`, `image-ingress.yaml`. All are conditional on their
values and use `.Release.Namespace`, not the old hardcoded namespace.

Storage is fully declarative: two `Bucket` CRs (`adoptExisting`, `Retain`,
`anonymousRead` on the public one), `S3Identity`, `S3Policy` +
`S3PolicyBinding` for the app actions, and `S3Credentials` writing
`accessKey`/`secretKey` into `gallery-s3`. The policy/binding is used instead of
`Bucket.spec.access` because the operator explicitly waits for the identity on
`S3PolicyBinding`, removing the same-apply ordering risk.

Two latent platform problems were fixed while porting, plus one ingress bug:

- The old `Seaweed` manifest set `master.persistence.requests`, which operator
  0.1.44 ignores; the field is `persistence.resources.requests` and
  `persistence.enabled` was never set. Master persistence is now actually on.
- The filer had no volume, so its leveldb2 bucket/object metadata was lost on
  restart. Filer persistence is now on (`filerPersistence`/`filerStorage`).
- The image ingress was missing `proxy-body-size`, so a direct 50 MB original
  PUT through ingress-nginx would have been a 413. Both this and the API ingress
  now allow 55 MB.

Verified with renders: full install emits all CRs and the image ingress with
`nginx.ingress.kubernetes.io/proxy-body-size: "55m"`; custom names
(`seaweed.name=weed`, `postgres.name=db`) and custom buckets propagate to every
reference; `platform.seaweedfs.enabled=false` removes the Seaweed, bucket, IAM
and image-ingress resources.

