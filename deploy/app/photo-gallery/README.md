# Photo Gallery Helm chart

Application workloads and the public site/API edge for Photo Gallery. The chart
targets Helm 4 (`apiVersion: v2`) and Kubernetes 1.27 or later.

It creates:

- an API `Deployment` and `Service` on port 8080;
- a web `Deployment` and `Service` on port 3000;
- a `ConfigMap` holding the API's non-secret configuration;
- an `Ingress` for the site host and another for the `api.` host, both with
  cert-manager annotations and per-host TLS secrets.

It deliberately does **not** create:

- the `img.` ingress (platform-owned, see `deploy/platform/seaweedfs/ingress.yaml`);
- any Secret, or any secret value;
- any CRD or custom resource (Postgres `Cluster`, SeaweedFS `Seaweed`,
  `ClusterIssuer`, certificates, buckets, IAM identities);
- any namespace or operator.

## Deployment order

The chart assumes the platform is already in place. Apply things in this order:

1. **cert-manager** and the `letsencrypt-prod` `ClusterIssuer`.
2. **CloudNativePG**, the Postgres `Cluster`, and the `pg-app` Secret it generates.
3. **SeaweedFS operator**, the `Seaweed` CR, both buckets, the anonymous
   `photos` read/list grant, the API's S3 identity, and the platform `img.`
   ingress.
4. **Application Secret** `gallery-secrets` in the release namespace, containing
   the six keys listed below.
5. **This chart**.

`Chart.yaml` cannot express a dependency on an operator release or on a CRD, so
`helm upgrade --install` of this chart before steps 1–4 fails in confusing ways:
missing Secrets, missing Services, and unissued certificates. The full sequence
is in `docs/deploy/runbook.md`.

All workloads, the Secret, and the platform `seaweed-s3` Service must be in the
same namespace. The default `config.storageEndpoint` assumes that Service name.

## Install

```bash
helm upgrade --install gallery deploy/app/photo-gallery \
  --namespace photo-gallery \
  --set image.api.repository=$REGISTRY/photo-gallery/api \
  --set image.web.repository=$REGISTRY/photo-gallery/web \
  --set image.api.tag="$GITHUB_SHA" \
  --set image.web.tag="$GITHUB_SHA" \
  --set ingress.siteHost=gallery.example.com \
  --set ingress.apiHost=api.example.com \
  --wait --timeout 10m
```

`ingress.imageHost` defaults to `img.<ingress.siteHost>`; set it if the platform
ingress uses a different name. The image repositories default to
`registry.example.com/...`, a placeholder that will not pull; set them to your
registry's real path (and create the pull secret — see below).

## Required values

These have the placeholder `__required__` in `values.yaml`.
`templates/validation.yaml` refuses to render an install or upgrade until all
four are replaced, so a forgotten tag or hostname fails at `helm template` time,
not at image-pull or DNS time:

| Value | Meaning |
| --- | --- |
| `image.api.tag` | Immutable API image tag. `latest` is rejected by the schema. |
| `image.web.tag` | Immutable web image tag. `latest` is rejected by the schema. |
| `ingress.siteHost` | Public site hostname, e.g. `gallery.example.com`. |
| `ingress.apiHost` | Public API hostname, e.g. `api.example.com`. |

The placeholder exists because `helm lint` validates `values.yaml` against the
schema: making the defaults empty would make the bare lint command fail. The
validation template keeps the deployment behaviour strict anyway.

## Values

### Images

| Value | Default | Description |
| --- | --- | --- |
| `image.api.repository` | `registry.example.com/photo-gallery/api` | Full repository path in the private registry. |
| `image.api.tag` | `__required__` | API image tag. Required; must not be `latest`. |
| `image.web.repository` | `registry.example.com/photo-gallery/web` | Full repository path in the private registry. |
| `image.web.tag` | `__required__` | Web image tag. Required; must not be `latest`. |
| `image.pullPolicy` | `IfNotPresent` | Pull policy for both images. |
| `image.pullSecrets` | `[photo-gallery-registry]` | `imagePullSecrets` for the private registry. |

### Private registry

The images live in the cluster's own registry, which requires basic auth. Create
the pull secret once per namespace before installing:

```bash
kubectl -n photo-gallery create secret docker-registry photo-gallery-registry \
  --docker-server="$REGISTRY" \
  --docker-username="$REGISTRY_USER" \
  --docker-password="$REGISTRY_PASSWORD"
```

If the registry's certificate is not publicly trusted, the nodes must trust it
before any pod can pull — that is a containerd/registries.yaml change, not
something this chart can do. Symptom: `x509: certificate signed by unknown
authority` in the pod events.

### Workloads

| Value | Default | Description |
| --- | --- | --- |
| `api.replicas` | `2` | API replica count. |
| `api.resources` | requests `100m`/`256Mi`, limits `1`/`1Gi` | API resource requests and limits. The image pipeline decodes originals up to 50 MB in process, so the limit is above a plain web service's. |
| `web.replicas` | `2` | Web replica count. |
| `web.port` | `3000` | Web container and Service port. |
| `web.healthPath` | `/healthz` | Both web probes use this path. See the contract below. |
| `web.resources` | requests `100m`/`128Mi`, limits `500m`/`256Mi` | Web resource requests and limits. |
| `web.publicSiteUrl` | `""` | `PUBLIC_SITE_URL`. Empty derives `https://<ingress.siteHost>`. |
| `web.imageBaseUrl` | `""` | `IMAGE_BASE_URL`. Empty derives `https://<image host>`. |
| `podAntiAffinity` | `preferred` | `preferred` keeps replicas off the same node when possible; `required` forbids sharing a node. |
| `podSecurityContext.runAsUser` | `1000` | UID for both pods. |
| `podSecurityContext.runAsGroup` | `1000` | GID for both pods. |
| `podSecurityContext.fsGroup` | `1000` | fsGroup for mounted volumes. |

Both Deployments use `maxUnavailable: 0` and `maxSurge: 1`. Pods run non-root,
with a read-only root filesystem, `allowPrivilegeEscalation: false`, all
capabilities dropped, and `seccompProfile: RuntimeDefault`. Only `/tmp` is
mounted writable (an `emptyDir`). Images must be readable by UID 1000; override
`podSecurityContext` if an image requires another UID.

`preferred` anti-affinity is the default so a single-node kind/k3s cluster can
run two replicas. Set `podAntiAffinity=required` in production if sharing a node
is unacceptable; on a single-node cluster that setting leaves the second replica
Pending.

### API configuration

| Value | Default | ConfigMap key |
| --- | --- | --- |
| `config.storageEndpoint` | `http://seaweed-s3:8333` | `Storage__Endpoint` |
| `config.storagePublicBucket` | `photos` | `Storage__PublicBucket` |
| `config.storagePrivateBucket` | `originals` | `Storage__PrivateBucket` |
| `config.storageForcePathStyle` | `true` | `Storage__ForcePathStyle` |
| `config.authIssuer` | `photo-gallery` | `Auth__Issuer` |
| `config.authAudience` | `photo-gallery-admin` | `Auth__Audience` |
| `config.corsAllowedOrigin` | `""` | `Cors__AllowedOrigin`; empty derives `https://<ingress.siteHost>` |

`ASPNETCORE_URLS=http://+:8080` is fixed in the ConfigMap. Changing the
ConfigMap rolls the API pods because the pod template carries a checksum.

### Secrets

| Value | Default | Description |
| --- | --- | --- |
| `secrets.existingSecret` | `gallery-secrets` | Name of an existing Secret in the release namespace. |

The chart references these keys with `secretKeyRef` and never stores their
values:

| Key | Used for |
| --- | --- |
| `Storage__AccessKey` | SeaweedFS S3 access key for the API identity. |
| `Storage__SecretKey` | SeaweedFS S3 secret key. |
| `Admin__Username` | The single admin account name. |
| `Admin__PasswordHash` | PBKDF2 or Argon2 hash of the admin password. |
| `Auth__SigningKey` | HS256 signing key for the `pg_session` cookie. Rotating it logs everyone out. |
| `ConnectionStrings__Gallery` | Npgsql connection string for the Postgres `pg-app` credentials. |

The Secret must exist before the Deployment is rolled out. A missing Secret or
key gives `CreateContainerConfigError`, not a template error: the chart only
knows the name.

### Ingress and TLS

| Value | Default | Description |
| --- | --- | --- |
| `ingress.className` | `nginx` | `ingressClassName` for both Ingresses. |
| `ingress.siteHost` | `__required__` | Site hostname; cert-manager issues `<release>-site-tls`. |
| `ingress.apiHost` | `__required__` | API hostname; cert-manager issues `<release>-api-tls`. |
| `ingress.imageHost` | `""` | Image hostname. Empty derives `img.<ingress.siteHost>`. Used for `IMAGE_BASE_URL` and the verification notes. |
| `ingress.certManager.clusterIssuer` | `letsencrypt-prod` | Value of `cert-manager.io/cluster-issuer`. |

Both Ingresses route `/` with `pathType: Prefix` to their Service. The API
Ingress additionally sets `nginx.ingress.kubernetes.io/proxy-body-size: 55m` so
the application, not ingress-nginx's 1 MB default, enforces the 50 MB upload
limit.

TLS secret names are derived from the release name: `<release>-site-tls` and
`<release>-api-tls`. cert-manager creates them from the annotations.

The `letsencrypt-prod` ClusterIssuer's HTTP-01 solver uses an ingress class
(`nginx` in the runbook). If `ingress.className` is changed, the ClusterIssuer
solver must be changed to match, or certificate issuance hangs on the challenge.

### Naming

| Value | Default | Description |
| --- | --- | --- |
| `nameOverride` | `""` | Replaces the chart name in `app.kubernetes.io/name`. |
| `fullnameOverride` | `""` | Replaces the release-derived base name. |

Resource names are release-based: `<release>-api`, `<release>-web`,
`<release>-config`, `<release>-site-ingress`, `<release>-api-ingress`,
`<release>-site-tls`, and `<release>-api-tls`.

## Web health contract

The frontend image **must** expose:

```text
GET /healthz   -> 200
```

on the container port (3000 by default). The route must be unauthenticated,
answer from the Node process itself, and must not call the API, Postgres, or
SeaweedFS. Both the liveness and readiness probes use it, and the value
`web.healthPath` can point at another path if the frontend changes it.

Readiness deliberately does not depend on the database: a Postgres blip must not
take every web pod out of the Service and produce a site-wide 5xx. The API's
readiness endpoint (`/api/v1/health/ready`) checks only that required
configuration is present, for the same reason.

## Image host contract

`IMAGE_BASE_URL` points at the platform image host
(`https://img.<siteHost>` by default). That host is **not** part of this chart.
`deploy/platform/seaweedfs/ingress.yaml` owns it and proxies only `/photos` to
the S3 gateway. The chart never creates an ingress for it, never exposes
`/originals`, and never routes image bytes through the API.

Two checks must keep returning 404 after every platform or app change:

```bash
curl -sI "https://img.<siteHost>/?Action=ListUsers" | head -1   # 404
curl -sI "https://img.<siteHost>/originals/" | head -1          # 404
```

`helm install` prints the same commands in its notes.

## Verifying

```bash
helm lint deploy/app/photo-gallery
helm template test deploy/app/photo-gallery \
  --set ingress.siteHost=gallery.example.com \
  --set ingress.apiHost=api.example.com \
  --set image.api.tag=test \
  --set image.web.tag=test
```

The second command omitting any required value fails with
`required values not set: ...`.

## Upgrades and secret rotation

The ConfigMap checksum rolls the API when non-secret configuration changes.
Secret contents are not checksummed: after rotating `Auth__SigningKey` or the
database password in `gallery-secrets`, run

```bash
kubectl -n photo-gallery rollout restart deploy/gallery-api
```

The default rolling strategy (`maxUnavailable: 0`) keeps at least one ready pod
in the Service during replacement. No PodDisruptionBudget is created.
