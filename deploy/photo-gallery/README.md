# Photo Gallery Helm chart

One chart for the whole gallery: the API, web, worker and notifier, plus the
Postgres cluster, SeaweedFS, its buckets and IAM, and the Traefik ingresses. It
uses the existing `ClusterIssuer` (default `letsencrypt-prod`) and targets
Helm 4 (`apiVersion: v2`) and Kubernetes 1.27 or later.

It deliberately does **not** install operators or CRDs. Install these first:

| Operator | Chart used by the runbook | CRDs consumed |
| --- | --- | --- |
| cert-manager | `jetstack/cert-manager` | `ClusterIssuer`, `Certificate` |
| CloudNativePG | `cnpg/cloudnative-pg` | `Cluster` |
| SeaweedFS operator | `seaweedfs-operator/seaweedfs-operator` 0.1.44+ | `Seaweed`, `Bucket`, `S3Identity`, `S3Policy`, `S3PolicyBinding`, `S3Credentials` |

Also required: **Traefik** as the Ingress controller, a default StorageClass,
DNS for the site/API/image hosts, and an existing `ClusterIssuer` with a Traefik
HTTP-01 solver (default name `letsencrypt-prod`). The ingress class is
hard-coded to `traefik`; the chart is not an nginx chart.

To create the issuer with the chart instead, set
`platform.certManager.issuer.create=true` and supply
`platform.certManager.issuer.email`. Leave it `false` when the cluster already
has one.

## Layout

```text
templates/
  api/         API Deployment/Service, API Ingress, migrate Job
  web/         Web Deployment/Service, site Ingress
  worker/      Worker Deployment
  notifier/    Notifier Deployment/Service
  postgres/    CloudNativePG Cluster
  seaweedfs/   Seaweed, Buckets/IAM, image Ingress
  cert-manager/ ClusterIssuer
  shared/      ConfigMap read by more than one workload
  secrets/     one file per Secret (app, postgres, storage, registry)
  NOTES.txt
  _helpers.tpl
  validation.yaml
```

Helm renders every folder; the split is for navigation only.

## Deployment order

1. **cert-manager**, **CloudNativePG**, **SeaweedFS operator** (the runbook has
   the versions).
2. **Traefik**, a default StorageClass, and DNS for `siteHost`, `apiHost` and
   `imageHost`.
3. **This chart** — it creates everything else.

`Chart.yaml` cannot express a dependency on an operator release or CRD. If an
operator is missing, the install fails on an unknown kind or a controller that
never reconciles; it does not fail cleanly in `helm template`.

## Install

The chart ships no `values.yaml`: it holds hosts and secrets, so it is
git-ignored per deployment. Copy the example and edit the `__required__`
placeholders, or pass the same values with `--set`:

```bash
cp deploy/photo-gallery/values.example.yaml deploy/photo-gallery/values.yaml
helm upgrade --install gallery deploy/photo-gallery --namespace photo-gallery \
  --create-namespace -f deploy/photo-gallery/values.yaml --wait --timeout 15m
```

The equivalent `--set` form:

```bash
helm upgrade --install gallery deploy/photo-gallery \
  --namespace photo-gallery --create-namespace \
  --set image.api.repository="$REGISTRY/gallery/api" \
  --set image.web.repository="$REGISTRY/gallery/web" \
  --set image.api.tag="$GITHUB_SHA" \
  --set image.web.tag="$GITHUB_SHA" \
  --set ingress.siteHost=gallery.example.com \
  --set ingress.apiHost=api.gallery.example.com \
  --set secrets.registry.username="$REGISTRY_USER" \
  --set-string secrets.registry.password="$REGISTRY_PASSWORD" \
  --set-string secrets.postgres.password="$POSTGRES_PASSWORD" \
  --set-string secrets.app.admin.passwordHash="$(dotnet run --no-launch-profile -v q --project backend/src/gallery/Gallery.Api -- hash-password 'the-admin-password')" \
  --wait --timeout 15m
```

The chart creates `photo-gallery-registry` from `secrets.registry`; the registry
host is derived from `image.api.repository`. If the registry is public, leave
both registry values empty and the chart creates no pull secret.

`secrets.app.admin.passwordHash` is the one value the chart cannot generate:
hashing needs the API's PBKDF2/Argon2 helper. The hash is safe to pass with
`--set`; the password itself never reaches the chart.

## Required values

`templates/validation.yaml` refuses to render an install or upgrade until these
are replaced. `helm lint` still runs with the placeholders so chart structure can
be checked without deployment values.

| Value | Meaning |
| --- | --- |
| `image.api.tag` | Immutable API image tag. `latest` is rejected by the schema. |
| `image.web.tag` | Immutable web image tag. `latest` is rejected by the schema. |
| `ingress.siteHost` | Public site hostname, e.g. `gallery.example.com`. |
| `ingress.apiHost` | Public API hostname, e.g. `api.gallery.example.com`. |
| `secrets.app.admin.passwordHash` | PBKDF2 or Argon2 hash of the admin password. |
| `secrets.postgres.password` | Postgres owner password; bootstraps CNPG and composes the DSN. |

Conditional requirements:

- `platform.certManager.issuer.email` when `platform.certManager.issuer.create=true`.
- `platform.postgres.connectionString` when `platform.postgres.enabled=false`.
- `config.storageEndpoint` when `platform.seaweedfs.enabled=false`.
- `secrets.storage.accessKey` **and** `secretKey` when
  `platform.seaweedfs.enabled=false`. With the operator, leave both empty and it
  generates the pair.
- `secrets.registry.username` and `.password` are both-or-neither.

## What it creates

- `Deployment`/`Service` for the API, web, worker and notifier.
- `ConfigMap` with the API's non-secret configuration.
- `Secret` `photo-gallery-registry` when registry credentials are set.
- `Secret` `gallery-secrets` (DSN, admin user/hash, signing key),
  `pg-bootstrap` (Postgres owner), and optionally `gallery-s3` (storage keys).
- CloudNativePG `Cluster` `pg` (one instance, 20 Gi by default).
- SeaweedFS `Seaweed` `seaweed`, plus the two `Bucket` CRs (names from
  `config.storagePublicBucket`/`storagePrivateBucket`),
  `S3Identity` `gallery-api`, `S3Policy`/`S3PolicyBinding` and `S3Credentials`.
- The three Traefik ingresses, annotated with the existing `ClusterIssuer`
  named by `platform.certManager.issuer.name`; the issuer itself is only
  created when `platform.certManager.issuer.create=true`.

All resources are namespaced into the release namespace, except an optionally
created `ClusterIssuer`. One release per cluster is assumed: `pg`, `seaweed`,
the image ingress and the default bucket/issuer names match the old install so
it can be adopted.

## Secrets

Each Secret has its own file under `templates/secrets/`; its name and values sit
in the same block.

| Value | Default | Description |
| --- | --- | --- |
| `secrets.registry.secretName` | `photo-gallery-registry` | Image pull Secret. |
| `secrets.registry.username` | `""` | Registry user. Set with `.password` to create the Secret. |
| `secrets.registry.password` | `""` | Registry password. |
| `secrets.app.secretName` | `gallery-secrets` | App Secret: DSN, admin login, signing key. |
| `secrets.app.signingKey` | `""` | HS256 key, 32+ characters. Generated when empty. |
| `secrets.app.admin.username` | `admin` | Admin login. |
| `secrets.app.admin.passwordHash` | `__required__` | PBKDF2/Argon2 hash; see the install command. |
| `secrets.postgres.secretName` | `pg-bootstrap` | CNPG initdb Secret. |
| `secrets.postgres.username` | `gallery` | Initdb owner and DSN user. |
| `secrets.postgres.database` | `gallery` | Initdb database. |
| `secrets.postgres.password` | `__required__` | Owner password; required, not generated. |
| `secrets.storage.secretName` | `gallery-s3` | Operator-generated `accessKey`/`secretKey`. |
| `secrets.storage.identity` | `gallery-api` | IAM user the API/worker authenticate as. |
| `secrets.storage.accessKey` | `""` | Set with `.secretKey` to choose the pair; empty lets the operator generate it. |
| `secrets.storage.secretKey` | `""` | See above. |

The chart creates `gallery-secrets`, `pg-bootstrap`, `photo-gallery-registry`
(when configured) and, when storage keys are supplied, `gallery-s3`. With empty
storage keys the operator creates `gallery-s3`. `gallery-secrets` does not
contain storage keys; the API/worker read `Storage__AccessKey`/`SecretKey` from
`gallery-s3`.

`secrets.app.signingKey` may be set explicitly. When empty, the chart generates
it on first install and reads the existing value back with `lookup` on upgrade.
`secrets.postgres.password` is required because it has to be rendered into both
the bootstrap Secret and the DSN.

The Postgres password is only used at cluster bootstrap. Changing
`secrets.postgres.password` later rewrites the Secret and the DSN but **does not
change the role's password**; use `ALTER ROLE` in Postgres and then update the
Secret. The same is true for a cluster adopted from the old manifests: keep the
value equal to the existing password, or pin
`platform.postgres.connectionString` to the existing DSN.

## Values

### Images

| Value | Default | Description |
| --- | --- | --- |
| `image.api.repository` / `.tag` | `registry.hatal.cc/gallery/...`, `__required__` | API image. The worker and notifier reuse it. |
| `image.web.repository` / `.tag` | `registry.hatal.cc/gallery/...`, `__required__` | Web image. |
| `image.pullPolicy` | `IfNotPresent` | Pull policy for both images. |

### Workloads

| Value | Default | Description |
| --- | --- | --- |
| `api.replicas` | `2` | API replica count. |
| `api.resources` | requests `100m`/`256Mi`, limits `1`/`1Gi` | API resources. |
| `worker.replicas` | `1` | Image worker replicas. Every replica competes for queue messages. |
| `worker.resources` | requests `250m`/`256Mi`, limits `2`/`2Gi` | Render resources. |
| `notifier.replicas` | `1` (schema-enforced) | Exactly one: it consumes the status queue. |
| `notifier.resources` | requests `50m`/`128Mi`, limits `500m`/`512Mi` | Stream resources. |
| `web.replicas` | `2` | Web replica count. |
| `web.resources` | requests `100m`/`128Mi`, limits `500m`/`256Mi` | Web resources. |
| `podAntiAffinity` | `preferred` | `required` leaves a second replica Pending on a single-node cluster. |

The API and web Deployments use `maxUnavailable: 0` / `maxSurge: 1`; the worker
and notifier use `maxUnavailable: 1` / `maxSurge: 0`.

The API, worker, notifier and migrate Job get a `wait-for-postgres` initContainer
when the chart owns Postgres. It calls `pg_isready` for up to five minutes, then
exits non-zero rather than hanging.

### Platform

| Value | Default | Description |
| --- | --- | --- |
| `platform.postgres.enabled` | `true` | Create the CNPG `Cluster`. |
| `platform.postgres.storage` | `20Gi` | Instance volume size. |
| `platform.postgres.connectionString` | `""` | Overrides the composed DSN; required when `enabled=false`. |
| `platform.seaweedfs.enabled` | `true` | Create the `Seaweed`, buckets, IAM and image ingress. |
| `platform.seaweedfs.image` | `chrislusf/seaweedfs:4.50` | Must match the operator's expected image. |
| `platform.seaweedfs.storage` | `100Gi` | Object data per volume server. |
| `platform.seaweedfs.maxVolumeCounts` | `32` | Volume slots per volume server. Each bucket needs a 7-volume batch, so keep this above two batches. |
| `platform.certManager.issuer.create` | `false` | Create the ClusterIssuer; default uses the existing one. |
| `platform.certManager.issuer.name` | `letsencrypt-prod` | Existing/new ClusterIssuer name; ingress annotations follow it. |
| `platform.certManager.issuer.email` | `""` | ACME email; required only when `create=true`. |
| `platform.imageIngress.enabled` | `true` | Publish `/photos` and `/originals` on `ingress.imageHost`. |
| `config.storageEndpoint` | `""` | Required when `seaweedfs.enabled=false`; otherwise derives `http://seaweed-s3:8333`. |
| `config.storagePublicBucket` | `photos` | Public derivatives bucket, `Bucket` CR name and image path. |
| `config.storagePrivateBucket` | `originals` | Private originals bucket, `Bucket` CR name and image path. |
| `ingress.siteHost` / `.apiHost` | `__required__` | Public site and API hosts. |
| `ingress.imageHost` | `""` | Empty derives `img.<siteHost>`. |

The SeaweedFS topology is fixed: one master, one filer, one S3 gateway, one
volume server, eight volume slots, persistence for master/filer and
`volumeServerDiskCount: 1`. The
Postgres cluster is one instance. Both use the cluster's default StorageClass;
the per-platform `storageClassName` knobs were removed because they are
cluster-specific.

### Ingress and TLS

All three Ingresses use `ingressClassName: traefik` and
`cert-manager.io/cluster-issuer: <platform.certManager.issuer.name>` (default
`letsencrypt-prod`, which must already exist). TLS secret names:
`<release>-site-tls`, `<release>-api-tls` and `img-tls`.

Traefik streams responses, so the notifier's SSE stream needs no buffering
annotations. Traefik also has no default request-body limit, so direct 50 MB
uploads to `img.<siteHost>` pass through. If your Traefik install applies a
global body limit, exempt that host or raise the limit there.

## Adopting an install made by the old split manifests

The chart defaults the Secret names to the old ones, so an existing install can
be moved into it. Keep the `secrets.*.secretName` values at their defaults and
add Helm ownership metadata first:

```bash
NS=photo-gallery REL=gallery

for r in cluster.postgresql.cnpg.io/pg seaweed.seaweedfs.com/seaweed \
         ingress/seaweed-s3-public \
         secret/gallery-secrets secret/pg-bootstrap secret/photo-gallery-registry; do
  kubectl -n "$NS" label "$r" app.kubernetes.io/managed-by=Helm --overwrite
  kubectl -n "$NS" annotate "$r" \
    meta.helm.sh/release-name="$REL" \
    meta.helm.sh/release-namespace="$NS" --overwrite
done
```

The default `platform.certManager.issuer.create=false` means the chart does not
own the ClusterIssuer, so there is nothing to adopt for it. If you set
`create=true` while the issuer name already exists, adopt it first or choose a
new name.

Then run the install command. Keep `secrets.postgres.password` equal to the
password in the existing `pg-bootstrap` (or set
`platform.postgres.connectionString` to the old DSN). The chart reads the
existing signing key out of `gallery-secrets`, so admin sessions survive.

The old `gallery` IAM user and its keys are left in SeaweedFS but unused: the
chart creates `gallery-api` and `gallery-s3` through the operator. Buckets are
adopted by name. Remove the old user with `weed shell` if you want the old keys
gone.

One migration caveat: the old hand-written `Seaweed` manifest set
`master.persistence.requests`, which operator 0.1.44 ignores, never set
`persistence.enabled`, and left the filer without a volume. The chart now sets
`enabled: true` with the correct `resources.requests` for both master and filer.
Claim templates cannot be added in place, so the operator reports a mismatch and
the pods keep running without persistence.

The master is safe to recreate; volume IDs survive and raft state is rebuilt:

```bash
kubectl -n photo-gallery delete statefulset seaweed-master --cascade=orphan
```

Do **not** recreate the filer blindly. The old filer had no volume, so its
leveldb2 bucket/object namespace lives only in the pod. Recreating the
StatefulSet drops it and would orphan existing objects even though the volume
servers still hold the bytes. The chart sets persistence on for new installs;
on an existing install the operator reports a claim-template mismatch and leaves
the filer as it is. Plan a metadata migration (or accept re-uploading) before
recreating that StatefulSet.

## Web health contract

The frontend image **must** expose:

```text
GET /healthz   -> 200
```

on port 3000. The route must be unauthenticated, answer from the Node process
itself, and must not call the API, Postgres, or SeaweedFS. Both probes use it.

The API's readiness endpoint (`/api/v1/health/ready`) and the notifier's check
only that required configuration is present. The notifier's liveness stays green
without it on purpose, so a misconfigured notifier shows as `0/1 Ready`, not as a
restart loop.

## Verifying

```bash
helm lint deploy/photo-gallery
helm template test deploy/photo-gallery \
  --set-string image.api.tag=test --set-string image.web.tag=test \
  --set ingress.siteHost=gallery.example.com \
  --set ingress.apiHost=api.example.com \
  --set-string secrets.postgres.password=the-db-password \
  --set-string secrets.app.admin.passwordHash='<hash>' \
  | grep -E '^kind:'
```

Omitting any required value fails with `required values not set: ...`.

## Upgrades and secret rotation

- **Registry credentials**: update `secrets.registry.*`; the chart updates the
  pull secret. The next pod start uses it.
- **Signing key**: set `secrets.app.signingKey` and roll the API and notifier.
  Rotating it logs every admin out.
- **Admin hash**: set `secrets.app.admin.passwordHash` and roll the API.
- **S3 keys**: with `secrets.storage.*` set the chart owns `gallery-s3` and the
  operator adopts it; with them empty the operator generates and rotates the
  pair. Roll the API and worker after a manual key change.
- **Postgres password**: update the role with SQL, then
  `secrets.postgres.password` (or pin `platform.postgres.connectionString`).
  The bootstrap Secret does not change an existing role.

No PodDisruptionBudget is created. The API and web keep at least one ready pod
during a rollout; the worker and notifier drain instead.
