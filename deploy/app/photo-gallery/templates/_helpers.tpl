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
The image host is platform-owned (deploy/platform/seaweedfs/ingress.yaml). The
chart only needs its name to build IMAGE_BASE_URL, and derives the conventional
img.<siteHost> name unless the operator overrides it.
*/}}
{{- define "photo-gallery.imageHost" -}}
{{- default (printf "img.%s" .Values.ingress.siteHost) .Values.ingress.imageHost -}}
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
  - name: Storage__AccessKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: Storage__AccessKey
  - name: Storage__SecretKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: Storage__SecretKey
  - name: Admin__Username
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: Admin__Username
  - name: Admin__PasswordHash
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: Admin__PasswordHash
  - name: Auth__SigningKey
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: Auth__SigningKey
  - name: ConnectionStrings__Gallery
    valueFrom:
      secretKeyRef:
        name: {{ .Values.secrets.existingSecret }}
        key: ConnectionStrings__Gallery
{{- end -}}
