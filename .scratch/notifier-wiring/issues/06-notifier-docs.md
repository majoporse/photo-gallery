# 06 — Correct the notifier docs and comments

Status: ready-for-agent
Blocked by: none

## Context

The split left text describing the old behaviour:

- `frontend/app/routes/admin-collection.tsx:359` says one stream carries the
  initial snapshot; the stream is deltas only and the snapshot comes from
  `GET /api/v1/admin/uploads`.
- The root README stack table has no notifier row, and the native-run block calls
  it "optional" even though live status depends on it.
- `backend/README.md` opens with "Two services".
- `values.yaml` says the notifier "has no database"; it has no database *of its
  own*, but it reads the queue from Postgres.
- `NOTES.txt` and the runbook verify API and web rollouts but not the notifier,
  and never check that the stream route answers 401 rather than 404.

## Scope

- Fix the stale comment and the service counts.
- Document the notifier's URL in the README run instructions.
- Correct the "no database" wording.
- Add the notifier rollout and a stream authentication check to `NOTES.txt` and
  the runbook's verify step.

## Done when

- No document describes the stream as carrying a snapshot.
- Every startup path in the README names the notifier and its port where relevant.
- `NOTES.txt` and the runbook include a notifier check.

## Comments

Implemented 2026-10-29.

- The stream comment now says deltas only, snapshot on open.
- Root README gains a Notifier row, the stream port in the run line, and drops
  the misleading "optional" label.
- `backend/README.md` says three hosts.
- `values.yaml` and `compose.yaml` describe the notifier as owning no tables,
  with the queue in Postgres.
- Chart README documents `web.publicApiUrl`, the notifier readiness behaviour,
  and adds the notifier to the signing-key rotation restart. Its rollout-strategy
  paragraph now distinguishes API/web from worker/notifier.
- `NOTES.txt` and the runbook rollout the notifier and check that the stream
  answers 401, not 404.

