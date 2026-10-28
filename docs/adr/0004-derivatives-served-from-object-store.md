---
Status: superseded in part by ADR 0006 — `originals` is now reachable from the internet as a write-only prefix.
---

# Derivatives are served straight from the object store through a path-restricted ingress

Images must be cacheable forever at content-addressed URLs, which means the browser should not fetch them through the API. But SeaweedFS serves its S3 API and its embedded **IAM API on the same port**, so publishing the gateway at `img.` would put an authentication endpoint on the internet for the sake of static files. We expose only the object-read path: the ingress forwards `/photos` on the image host to the S3 gateway and answers nothing else, and the API is never in the image path.

**Consequences.** Two buckets, because SeaweedFS grants anonymous access per bucket rather than per prefix: `photos` holds derivatives and grants `anonymous` read+list, while `originals` is never referenced by any public route. Public reads therefore bypass application authentication entirely, and the separation rests on the ingress rule — so CI asserts that `/originals/...` and `/?Action=...` return 404 on the image host.

**Rejected.** Proxying every image through the .NET API — keeps one public surface, but puts a .NET process in the hot path for bytes it contributes nothing to. A dedicated read-only proxy service — a third thing to deploy for no capability the gateway lacks. Bucket policy denying `originals/*` explicitly — plausible on paper, but it leans on anonymous-principal handling of resource-level `Deny` that is not worth trusting when a second bucket removes the question.
