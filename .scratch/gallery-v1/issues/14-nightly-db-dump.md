# 14 — Nightly Postgres dump

Status: needs-triage
Blocked by: none

## Context
Deliberately deferred during design, recorded here so it is not forgotten. The NAS copy of the originals is the archive of record, which makes the cluster disposable — except for Postgres, which holds the only copy of collection names, orderings, captions, featured ranks and EXIF. If that PVC dies, the photographs survive and the gallery does not.

## Scope (proposed)
- A CronJob running `pg_dump --format=custom` against the CloudNativePG primary, written to a NAS mount, retaining 14 daily copies.
- A restore procedure in `docs/deploy/runbook.md`, tested once against a scratch database.
- No bucket backup: derivatives are rebuildable from originals, and originals are the NAS's job.

## Done when
- A dump appears nightly and a restore into an empty database reproduces collections, photos and ordering.
- The restore procedure is documented step by step, including how to verify counts.
