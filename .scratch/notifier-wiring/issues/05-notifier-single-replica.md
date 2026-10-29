# 05 — Pin the notifier to one replica

Status: ready-for-agent
Blocked by: none

## Context

ADR 0010 requires exactly one notifier, because it is a queue consumer: a second
replica takes half the events and never sees the first replica's subscribers.
`values.yaml` and the chart README say this, but `values.schema.json` accepts
`notifier.replicas >= 1`, so `--set notifier.replicas=2` renders happily and
silently breaks delivery.

## Scope

- Constrain `notifier.replicas` to `1` in `values.schema.json`.
- Keep the value so the template and README stay explicit about the intent.

## Done when

- `helm template` with `--set notifier.replicas=2` fails schema validation.
- The default render is unchanged.

## Comments

Implemented 2026-10-29.

`notifier.replicas` is now `const: 1` in `values.schema.json`, with the reason in
the schema description. Verified: `helm template --set notifier.replicas=2`
fails with `at '/notifier/replicas': value must be 1`; the default render is
unchanged.

