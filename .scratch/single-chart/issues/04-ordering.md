# 04 — Gate workloads on Postgres and keep hooks ordered

Status: ready-for-agent
Blocked by: 02, 03

## Context
The chart now creates the database in the same release. The API, worker,
notifier and migrate Job currently start before `pg-rw` exists and crash-loop
until CNPG finishes bootstrapping.

## Scope
- Add a `wait-for-postgres` initContainer (bounded `pg_isready` loop, 5 minutes)
  to API, worker, notifier and the migrate Job when the chart owns Postgres.
- Keep the migrate Job as `post-install,pre-upgrade`; it now waits for the
  database instead of failing.
- Leave it out when `platform.postgres.enabled=false`.

## Done when
- `helm template` shows the initContainer in all four pods and none in app-only
  mode.
- A timeout exits non-zero rather than hanging forever.

## Comments

Implemented 2026-10-29.

`photo-gallery.waitForPostgres` emits a `pg_isready` initContainer with a
150-iteration, 2-second loop (five minutes), then exits non-zero. It is included
in the API, worker, notifier and migrate Job when
`platform.postgres.enabled=true`, and omitted otherwise.

The migrate Job keeps `post-install,pre-upgrade`; it now waits for `pg-rw`
instead of failing immediately. The API/worker/notifier also wait, so the first
install does not produce crash loops while CNPG bootstraps.

Verified: the full render has four `wait-for-postgres` initContainers; the
app-only render has none and the script is not emitted.

