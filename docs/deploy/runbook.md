# Operator runbook

Every command is copy-pasteable and idempotent (`helm upgrade --install`, `kubectl apply`). Run them in order. Written for Helm 4 and a single cluster with an `ingress-nginx` controller.

Three things that bite people, up front:

- **A chart version is not the operator version.** CloudNativePG chart `0.29.1` installs operator `1.30.1`. Always read `appVersion` in the chart index before assuming.
- **`Chart.yaml` cannot declare an external CRD dependency.** `dependencies:` only pulls subcharts, and `crds/` is install-only and never upgraded. Operators are separate releases, installed before anything that creates their custom resources.
- **Some charts ship CRDs in `crds/`, some as templates.** cert-manager needs `--set crds.enabled=true`; the SeaweedFS operator templates its CRD so `helm upgrade` can actually update it.

Versions below were verified against the chart indexes on 2026-10-25.

## 0. Prerequisites

`kubectl` pointed at the target cluster, Helm 4.x, and DNS for `example.com`, `api.example.com`, `img.example.com` pointing at the ingress address. Set two shell variables:

```bash
export DOMAIN=example.com
export NS=photo-gallery
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

## 2. ClusterIssuer

```yaml
# deploy/platform/cert-manager/cluster-issuer.yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: you@example.com # change
    privateKeySecretRef:
      name: letsencrypt-prod-account
    solvers:
      - http01:
          ingress:
            ingressClassName: nginx
```

```bash
kubectl apply -f deploy/platform/cert-manager/cluster-issuer.yaml
kubectl wait --for=condition=Ready clusterissuer/letsencrypt-prod --timeout=120s
```

## 3. CloudNativePG

```bash
helm repo add cnpg https://cloudnative-pg.github.io/charts && helm repo update
helm upgrade --install cnpg cnpg/cloudnative-pg \
  --namespace cnpg-system --create-namespace \
  --version 0.29.1
kubectl -n cnpg-system rollout status deploy/cnpg-cloudnative-pg --timeout=180s
```

Bootstrap credentials are supplied by you; CloudNativePG then generates the `pg-app` Secret (host, port, dbname, user, password) that the API consumes.

```bash
kubectl -n "$NS" create secret generic pg-bootstrap \
  --type kubernetes.io/basic-auth \
  --from-literal=username=gallery \
  --from-literal=password="$(openssl rand -base64 24)" \
  --dry-run=client -o yaml | kubectl apply -f -
```

```yaml
# deploy/platform/postgres/cluster.yaml
apiVersion: postgresql.cnpg.io/v1
kind: Cluster
metadata:
  name: pg
  namespace: photo-gallery
spec:
  instances: 1
  storage:
    size: 20Gi
  bootstrap:
    initdb:
      database: gallery
      owner: gallery
      secret:
        name: pg-bootstrap
```

```bash
kubectl apply -f deploy/platform/postgres/cluster.yaml
kubectl -n "$NS" wait --for=condition=Ready cluster/pg --timeout=600s
kubectl -n "$NS" get secret pg-app -o jsonpath='{.data.host}' | base64 -d; echo
```

## 4. SeaweedFS operator and cluster

```bash
helm repo add seaweedfs-operator https://seaweedfs.github.io/seaweedfs-operator/
helm repo update
helm upgrade --install seaweedfs-operator seaweedfs-operator/seaweedfs-operator \
  --namespace seaweedfs-system --create-namespace \
  --version 0.1.44
kubectl -n seaweedfs-system rollout status deploy/seaweedfs-operator --timeout=180s
```

Note the second S3 port: the operator exposes a standalone gateway Service named `<name>-s3` on **8333**. Do not expose the filer's own S3 listener as well; one writer path is enough.

```yaml
# deploy/platform/seaweedfs/seaweed.yaml
apiVersion: seaweed.seaweedfs.com/v1
kind: Seaweed
metadata:
  name: seaweed
  namespace: photo-gallery
spec:
  image: chrislusf/seaweedfs:4.50
  volumeServerDiskCount: 1
  master:
    replicas: 1
    persistence:            # off by default; without it every restart mints a new TopologyId
      requests:
        storage: 1Gi
  volume:
    replicas: 1
    requests:
      storage: 100Gi
  filer:
    replicas: 1
    config: |
      [leveldb2]
      enabled = true
      dir = "/data/filerldb2"
  s3:
    replicas: 1
```

If your storage class is not the cluster default, add `volume.storageClassName` (and the same on the master) before applying — volume claim templates are immutable, so a later edit means deleting the StatefulSet.

```bash
kubectl apply -f deploy/platform/seaweedfs/seaweed.yaml
kubectl -n "$NS" get seaweed,po -w   # wait until seaweed-s3 and seaweed-filer are Ready
```

Create both buckets and the API's own identity. Confirm the exact command names with `weed shell -h` in your version before trusting the transcript:

```bash
kubectl -n "$NS" get pods | grep filer   # pick the pod; it may be a StatefulSet or a Deployment
kubectl -n "$NS" exec -it <filer-pod> -- weed shell
# inside the shell:
#   s3.bucket.create -name photos      # derivatives, anonymous read
#   s3.bucket.create -name originals   # private, never exposed
#   s3.bucket.access -name photos -user anonymous -access Read,List
#   s3.configure -user gallery -access_key AKIA... -secret_key ... \
#       -actions Read,Write,List,Tagging -buckets photos,originals
```

Two buckets is not tidiness: SeaweedFS grants anonymous access per bucket, so a single bucket with a public policy would expose every original alongside its derivatives.

Then expose object reads and nothing else:

```bash
kubectl apply -f deploy/platform/seaweedfs/ingress.yaml
```

```bash
curl -sI "https://img.$DOMAIN/?Action=ListUsers"  | head -1   # 404 — IAM is not reachable
curl -sI "https://img.$DOMAIN/originals/"        | head -1    # 404 — bucket not exposed
curl -sI "https://img.$DOMAIN/photos/"           | head -1    # 200/403 until the first upload
```

> The operator also offers an `S3Identity` custom resource. Check `kubectl explain s3identity.spec` and prefer the CR if it covers what you need — it survives pod restarts as desired state, while shell commands do not.

## 5. Application secrets

CI owns these; to bootstrap by hand. The registry pull secret comes first — without it the pods cannot fetch their images at all:

```bash
kubectl -n "$NS" create secret docker-registry photo-gallery-registry \
  --docker-server="$REGISTRY" \
  --docker-username="$REGISTRY_USER" \
  --docker-password="$REGISTRY_PASSWORD" \
  --dry-run=client -o yaml | kubectl apply -f -
```

If the registry certificate is not publicly trusted, the nodes need it in containerd's trust store; the symptom is `x509: certificate signed by unknown authority` in pod events, and the `pullSecrets` entry will not help.

```bash
kubectl -n "$NS" create secret generic gallery-secrets \
  --from-literal=ConnectionStrings__Gallery="Host=pg-rw;Port=5432;Database=gallery;Username=gallery;Password=$(kubectl -n "$NS" get secret pg-app -o jsonpath='{.data.password}' | base64 -d)" \
  --from-literal=Storage__AccessKey=AKIA... \
  --from-literal=Storage__SecretKey=... \
  --from-literal=Admin__Username=admin \
  --from-literal=Admin__PasswordHash='<PBKDF2 hash>' \
  --from-literal=Auth__SigningKey="$(openssl rand -base64 48)" \
  --dry-run=client -o yaml | kubectl apply -f -
```

`Storage__Endpoint` is a plain ConfigMap value: `http://seaweed-s3:8333`, with `Storage__ForcePathStyle=true`.

## 6. The app

```bash
helm upgrade --install gallery deploy/app/photo-gallery \
  --namespace "$NS" \
  --set ingress.siteHost="$DOMAIN" \
  --set ingress.apiHost="api.$DOMAIN" \
  --set ingress.imageHost="img.$DOMAIN" \
  --set image.api.repository="$REGISTRY/photo-gallery/api" \
  --set image.web.repository="$REGISTRY/photo-gallery/web" \
  --set image.api.tag="$GITHUB_SHA" \
  --set image.web.tag="$GITHUB_SHA" \
  --wait --timeout 10m
```

The chart creates the API and web Deployments, their Services, three Ingresses (site, `api.`, `img.`), and the cert-manager annotations that mint certificates for each host.

## 7. Verify

```bash
kubectl -n "$NS" get pods,svc,ingress,certificate
kubectl -n "$NS" get cluster.postgresql.cnpg.io,seaweed
curl -sf "https://api.$DOMAIN/api/v1/health" && echo ok
curl -sI "https://$DOMAIN/" | head -1
curl -s "https://api.$DOMAIN/api/v1/home" | head -c 200; echo
```

A `Certificate` stuck in `False` is almost always DNS not yet pointing at the ingress, or the HTTP-01 path being swallowed by a catch-all rule.

## 8. Upgrade order

Operators first, then the app: cert-manager → CloudNativePG → SeaweedFS operator → `Cluster`/`Seaweed` CRs → app chart. Never upgrade an operator past the CRD version its CRs were written for without reading its release notes; that is the one upgrade that can orphan data.

This ordering is what CI runs, in that sequence, and it is why the app chart never assumes a CRD exists.
