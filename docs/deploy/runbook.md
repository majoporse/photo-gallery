# Operator runbook

Every command is copy-pasteable and idempotent (`helm upgrade --install`, `kubectl apply`). Run them in order. Written for Helm 4, Traefik as the Ingress controller, and a single cluster.

Three things that bite people, up front:

- **A chart version is not the operator version.** CloudNativePG chart `0.29.1` installs operator `1.30.1`. Always read `appVersion` in the chart index before assuming.
- **`Chart.yaml` cannot declare an external CRD dependency.** `dependencies:` only pulls subcharts, and `crds/` is install-only and never upgraded. Operators are separate releases, installed before anything that creates their custom resources.
- **Some charts ship CRDs in `crds/`, some as templates.** cert-manager needs `--set crds.enabled=true`; the SeaweedFS operator templates its CRD so `helm upgrade` can actually update it.

Versions below were verified against the chart indexes on 2026-10-25.

## 0. Prerequisites

`kubectl` pointed at the target cluster, Helm 4.x, Traefik installed as the Ingress controller, and DNS for `example.com`, `api.example.com`, `img.example.com` pointing at the ingress address. Set these shell variables:

```bash
export DOMAIN=example.com
export NS=photo-gallery
export REGISTRY=registry.hatal.cc
export REGISTRY_USER=...
export REGISTRY_PASSWORD=...
kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -
```

## 1. cert-manager

```bash
helm repo add jetstack https://charts.jetstack.io && helm repo update
helm upgrade --install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace \
  --version v1.21.3 \
  --set crds.enabled=true
```

Wait for it before continuing — an Issuer applied too early is simply retried, but the failure is confusing:

```bash
kubectl -n cert-manager rollout status deploy/cert-manager-webhook --timeout=180s
```

## 2. cert-manager issuer

The chart uses an existing `ClusterIssuer` by default; it does not own it. Check
that `letsencrypt-prod` exists and has a Traefik HTTP-01 solver:

```bash
kubectl get clusterissuer letsencrypt-prod
kubectl get clusterissuer letsencrypt-prod -o jsonpath='{.spec.acme.solvers[*].http01.ingress.class}{"\n"}'
# expect: traefik
```

To let the chart create a dedicated issuer on a fresh cluster, add
`--set platform.certManager.issuer.create=true --set
platform.certManager.issuer.email=you@example.com` to the install below.

## 3. CloudNativePG

```bash
helm repo add cnpg https://cloudnative-pg.github.io/charts && helm repo update
helm upgrade --install cnpg cnpg/cloudnative-pg \
  --namespace cnpg-system --create-namespace \
  --version 0.29.1
kubectl -n cnpg-system rollout status deploy/cnpg-cloudnative-pg --timeout=180s
```

The chart creates the `Cluster` and its bootstrap Secret `pg-bootstrap`, then
composes the DSN from `pg-rw` and `secrets.postgres.password`. The password is a
required value, not generated. Storage uses the cluster's default StorageClass;
the volume claim template is immutable.

CNPG still generates `pg-app`, but nothing reads it; `gallery-secrets` is the
app's source of truth.

## 4. SeaweedFS operator

```bash
helm repo add seaweedfs-operator https://seaweedfs.github.io/seaweedfs-operator/
helm repo update
helm upgrade --install seaweedfs-operator seaweedfs-operator/seaweedfs-operator \
  --namespace seaweedfs-system --create-namespace \
  --version 0.1.44
kubectl -n seaweedfs-system rollout status deploy/seaweedfs-operator --timeout=180s
```

The chart creates the `Seaweed` CR, both `Bucket` CRs (`photos`, `originals`),
the `gallery-api` `S3Identity`, its `S3Policy`/`S3PolicyBinding`, the
`gallery-s3` credentials Secret and the image ingress. `photos` is
anonymous-read; `originals` is not. `adoptExisting: true` lets it take over
buckets created by the old `weed shell` flow.

The operator exposes the S3 gateway as `seaweed-s3` on **8333**. The image
ingress publishes only the two bucket prefixes, so the IAM API on the same port
is not reachable from outside.

Storage uses the cluster's default StorageClass; volume claim templates are
immutable.

## 5. Registry credentials and passwords

The chart creates `gallery-secrets`, `pg-bootstrap`, `photo-gallery-registry`
and (through the operator or from supplied keys) `gallery-s3`. You supply the
values:

- **Registry user/password**: `secrets.registry.username` and `.password`. The
  chart derives the registry host from `image.api.repository` and creates the
  pull secret. If the registry certificate is not publicly trusted, the nodes
  need it in containerd's trust store; the symptom is `x509: certificate signed
  by unknown authority` in pod events.
- **Postgres owner password**: `secrets.postgres.password`. Required; it
  bootstraps CNPG and composes the DSN. On an existing cluster, use the current
  password.
- **Admin hash**: the password itself never goes into values; hash it with the
  API helper.

```bash
export POSTGRES_PASSWORD="$(openssl rand -base64 24)"
export ADMIN_PASSWORD_HASH="$(dotnet run --no-launch-profile -v q --project backend/src/gallery/Gallery.Api -- hash-password 'the-admin-password')"
```

or, without a checkout, from the API image:

```bash
export ADMIN_PASSWORD_HASH="$(docker run --rm "$REGISTRY/gallery/api:$GITHUB_SHA" hash-password 'the-admin-password')"
```

## 6. The app and the platform

```bash
helm upgrade --install gallery deploy/photo-gallery \
  --namespace "$NS" --create-namespace \
  --set ingress.siteHost="$DOMAIN" \
  --set ingress.apiHost="api.$DOMAIN" \
  --set ingress.imageHost="img.$DOMAIN" \
  --set image.api.repository="$REGISTRY/gallery/api" \
  --set image.web.repository="$REGISTRY/gallery/web" \
  --set image.api.tag="$GITHUB_SHA" \
  --set image.web.tag="$GITHUB_SHA" \
  --set secrets.registry.username="$REGISTRY_USER" \
  --set-string secrets.registry.password="$REGISTRY_PASSWORD" \
  --set-string secrets.postgres.password="$POSTGRES_PASSWORD" \
  --set-string secrets.app.admin.passwordHash="$ADMIN_PASSWORD_HASH" \
  --wait --timeout 15m
```

One release now owns the API, web, worker and notifier Deployments and Services,
the ConfigMap, the chart-owned Secrets (registry, app, Postgres bootstrap, and
storage when supplied) plus the operator's `gallery-s3` when storage keys are
empty, the CNPG `Cluster`, the `Seaweed`, both `Bucket`s, the
`S3Identity`/`S3Policy`/`S3PolicyBinding`/`S3Credentials`, and the three Traefik
Ingresses (site, `api.`, `img.`). It references the existing `letsencrypt-prod`
`ClusterIssuer`; operators and their CRDs are the only prior step.

Migrations run as a Helm hook Job (`<release>-api-migrate-<revision>`,
`post-install,pre-upgrade`) using the API image with the `migrate` argument. It
is a hook rather than an init container so two replicas cannot migrate
concurrently; `post-install` because the Job reads the release's ConfigMap and
the database it creates. The Job waits up to five minutes for `pg-rw` before
running. To inspect a failed run:

```bash
kubectl -n "$NS" get jobs
kubectl -n "$NS" logs job/gallery-api-migrate-1
```

## 7. Verify

```bash
kubectl -n "$NS" wait --for=condition=Ready cluster.postgresql.cnpg.io/pg --timeout=300s
kubectl -n "$NS" get seaweed,bucket,s3identity,s3policy,s3policybinding,s3credentials
kubectl -n "$NS" get secret gallery-secrets pg-bootstrap gallery-s3 photo-gallery-registry
kubectl -n "$NS" rollout status deploy/gallery-api --timeout=180s
kubectl -n "$NS" rollout status deploy/gallery-notifier --timeout=180s
kubectl -n "$NS" get pods,svc,ingress,certificate
curl -sf "https://api.$DOMAIN/api/v1/health" && echo ok
curl -sf "https://api.$DOMAIN/api/v1/health/ready" && echo ready
curl -s -o /dev/null -w '%{http_code}\n' "https://api.$DOMAIN/api/v1/admin/uploads/events"   # expect 401, not 404
curl -sI "https://$DOMAIN/" | head -1
curl -s "https://api.$DOMAIN/api/v1/home" | head -c 200; echo
```

The image host is part of the chart now. These are the access-control
assertions:

```bash
curl -sI "https://img.$DOMAIN/?Action=ListUsers"  | head -1   # 404 — IAM is not reachable
curl -sI "https://img.$DOMAIN/originals/"        | head -1    # 403 — no anonymous read
curl -sI "https://img.$DOMAIN/photos/"           | head -1    # 200/403 until the first upload
```

A `Certificate` stuck in `False` is almost always DNS not yet pointing at the
ingress, or the HTTP-01 path being swallowed by a catch-all rule.

## 8. Upgrade order

Operators first, then the chart: cert-manager → CloudNativePG → SeaweedFS
operator → `helm upgrade`. The chart creates the `Cluster`, `Seaweed`, buckets
and IAM; an existing issuer is referenced, not owned. Never upgrade an operator
past the CRD version its CRs were written for without reading its release notes;
that is the one upgrade that can orphan data.

This ordering is what CI runs, and it is why the chart assumes the CRDs exist.
