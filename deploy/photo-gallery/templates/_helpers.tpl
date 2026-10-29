{{/*
Chart name, overridable for installations that need a different label value.
*/}}
{{- define "photo-gallery.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Base resource name. Release-scoped, so two releases in one namespace do not
collide. fullnameOverride exists for installations that need a fixed name.
*/}}
{{- define "photo-gallery.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "photo-gallery.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.labels" -}}
helm.sh/chart: {{ include "photo-gallery.chart" . }}
{{ include "photo-gallery.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/*
Selector labels must not include chart version: Deployment selectors are
immutable, and a version bump must not require deleting the Deployment.
*/}}
{{- define "photo-gallery.selectorLabels" -}}
app.kubernetes.io/name: {{ include "photo-gallery.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "photo-gallery.api.fullname" -}}
{{- printf "%s-api" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.web.fullname" -}}
{{- printf "%s-web" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.notifier.fullname" -}}
{{- printf "%s-notifier" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.worker.fullname" -}}
{{- printf "%s-worker" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.config.fullname" -}}
{{- printf "%s-config" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.siteIngress.fullname" -}}
{{- printf "%s-site-ingress" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.apiIngress.fullname" -}}
{{- printf "%s-api-ingress" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/* cert-manager creates the Certificate with the same name as the TLS Secret. */}}
{{- define "photo-gallery.siteTlsSecret" -}}
{{- printf "%s-site-tls" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "photo-gallery.apiTlsSecret" -}}
{{- printf "%s-api-tls" (include "photo-gallery.fullname" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
The image host is platform-owned (same chart, image ingress) and derives from
the site host unless overridden.
*/}}
{{- define "photo-gallery.imageHost" -}}
{{- default (printf "img.%s" .Values.ingress.siteHost) .Values.ingress.imageHost -}}
{{- end -}}

{{/* The CNPG read-write Service for the fixed Cluster name. */}}
{{- define "photo-gallery.postgres.host" -}}
pg-rw
{{- end -}}

{{/* The standalone S3 gateway Service the operator creates for the CR. */}}
{{- define "photo-gallery.seaweed.s3Host" -}}
seaweed-s3
{{- end -}}

{{- define "photo-gallery.seaweed.endpoint" -}}
http://seaweed-s3:8333
{{- end -}}

{{- define "photo-gallery.clusterIssuer" -}}
{{- .Values.platform.certManager.issuer.name -}}
{{- end -}}

{{/*
The chart now creates Postgres in the same release, so the API, worker, notifier
and migrate Job would otherwise crash-loop until CNPG bootstraps. A bounded wait
turns that into a Pending pod; five minutes is longer than a first bootstrap.
*/}}
{{- define "photo-gallery.waitForPostgres" -}}
{{- if .Values.platform.postgres.enabled }}
initContainers:
  - name: wait-for-postgres
    image: postgres:18
    command:
      - sh
      - -c
      - |
        i=0
        until pg_isready -h {{ include "photo-gallery.postgres.host" . }} -p 5432; do
          i=$((i + 1))
          if [ "$i" -ge 150 ]; then
            echo "Postgres did not become ready in 5 minutes." >&2
            exit 1
          fi
          sleep 2
        done
    securityContext:
      runAsNonRoot: true
      # The postgres:18 image only has UID/GID 999. Running pg_isready as the
      # pod's app UID (1000) makes libpq fail with "no attempt".
      runAsUser: 999
      runAsGroup: 999
      allowPrivilegeEscalation: false
      readOnlyRootFilesystem: true
      capabilities:
        drop:
          - ALL
    volumeMounts:
      - name: tmp
        mountPath: /tmp
{{- end }}
{{- end -}}

{{/*
The notifier validates the admin session but never issues one, so it gets the
signing key and the queue, not the admin password.
*/}}
{{- define "photo-gallery.notifier.env" -}}
envFrom:
  - configMapRef:
      name: {{ include "photo-gallery.config.fullname" . }}
env:
  - name: Auth__SigningKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: Auth__SigningKey
  - name: ConnectionStrings__Gallery
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: ConnectionStrings__Gallery
{{- end -}}

{{/*
Soft anti-affinity keeps replicas on separate nodes when nodes are available but
still lets a single-node kind/k3s cluster schedule both replicas. Set
podAntiAffinity=required where a shared node is unacceptable.
*/}}
{{- define "photo-gallery.affinity" -}}
affinity:
  podAntiAffinity:
    {{- if eq .root.Values.podAntiAffinity "required" }}
    requiredDuringSchedulingIgnoredDuringExecution:
      - topologyKey: kubernetes.io/hostname
        labelSelector:
          matchLabels:
            {{- include "photo-gallery.selectorLabels" .root | nindent 12 }}
            app.kubernetes.io/component: {{ .component }}
    {{- else }}
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          topologyKey: kubernetes.io/hostname
          labelSelector:
            matchLabels:
              {{- include "photo-gallery.selectorLabels" .root | nindent 14 }}
              app.kubernetes.io/component: {{ .component }}
    {{- end }}
{{- end -}}

{{/*
Environment shared by the API Deployment and the migration Job, so the two
cannot drift apart.
*/}}
{{- define "photo-gallery.api.env" -}}
envFrom:
  - configMapRef:
      name: {{ include "photo-gallery.config.fullname" . }}
env:
  # The operator owns these keys, generates them on first use and rotates them
  # in this Secret. They are not part of gallery-secrets.
  - name: Storage__AccessKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.storage.secretName }}
        key: accessKey
  - name: Storage__SecretKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.storage.secretName }}
        key: secretKey
  - name: Admin__Username
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: Admin__Username
  - name: Admin__PasswordHash
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: Admin__PasswordHash
  - name: Auth__SigningKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: Auth__SigningKey
  - name: ConnectionStrings__Gallery
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.app.secretName }}
        key: ConnectionStrings__Gallery
{{- end -}}
