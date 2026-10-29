# Single deployment chart

Status: implemented. All six tickets are closed.

Today the cluster install is split: `deploy/app/photo-gallery` runs the app and
`deploy/platform/*` are raw manifests applied by hand (Postgres `Cluster`,
Seaweed `Seaweed`, `ClusterIssuer`, image ingress, buckets/IAM). This effort
turns that into one chart at `deploy/photo-gallery` that installs everything
except the operators and their CRDs, which remain prerequisites.

Decisions taken with the user on 2026-10-29:

- One chart at `deploy/photo-gallery`; delete `deploy/app` and `deploy/platform`.
- The chart creates all Secrets, including the Postgres bootstrap password and
  the signing key. The admin password hash stays a required value because a
  chart cannot hash a password.
- Storage buckets and IAM are declarative, using the SeaweedFS operator's
  `Bucket`, `S3Identity` and `S3Credentials` CRDs (present in chart 0.1.44).
- Operators (cert-manager, CloudNativePG, SeaweedFS) are installed beforehand;
  the chart only creates namespaced CRs and one ClusterIssuer.

## Tickets

| # | Title |
| - | ----- |
| 01 | Move the chart and merge platform values |
| 02 | Template Postgres, SeaweedFS, buckets/IAM, issuer and image ingress |
| 03 | Create the app Secret and switch storage credentials to the operator |
| 04 | Gate workloads on Postgres and keep hooks ordered |
| 05 | Update CI, runbook, READMEs; remove the split manifests |
| 06 | Verify renders, app-only mode and the adoption path |

## Non-goals

Installing operators or CRDs, TLS solver changes beyond ingress-nginx, and
multi-namespace resource sharing. The image ingress stays nginx-specific.
