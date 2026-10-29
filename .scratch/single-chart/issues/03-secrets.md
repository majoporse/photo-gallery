# 03 — Create the app Secret and switch storage credentials to the operator

Status: ready-for-agent
Blocked by: 01

## Context
The app Secret is currently created by CI or by hand from `pg-app`, which cannot
work when the chart creates Postgres in the same release. Storage keys currently
live in that Secret too; the operator can generate and rotate them.

## Scope
- `templates/secrets/` (one file per Secret) renders `gallery-secrets` with the DSN, admin user and
  hash, and signing key. Empty values are generated once and read back with
  `lookup` on upgrade; an existing Secret is reused.
- Render `pg-bootstrap` in the same template, with the same generated password
  the DSN uses.
- Allow `platform.postgres.connectionString` to override the composed DSN (for
  adoption or an external database).
- Point `Storage__AccessKey`/`Storage__SecretKey` at the operator-created
  `gallery-s3` Secret (`accessKey`/`secretKey`) in the API/worker env.
- Validate that a signing key supplied as a value is at least 32 characters.

## Done when
- `helm template` produces a working DSN and bootstrap Secret with matching
  passwords, and no storage keys in `gallery-secrets`.
- Two renders after an install would reuse the same values (lookup path).

## Comments

Implemented 2026-10-29.

`templates/secrets/` (one file per Secret) renders `gallery-secrets` (DSN, admin username/hash,
signing key) and `pg-bootstrap` (`kubernetes.io/basic-auth`, username/password).
Values win; otherwise `lookup` reuses the existing Secret, otherwise
`randAlphaNum` generates one. The DSN is composed from
`<platform.postgres.name>-rw` and the same password rendered into the bootstrap
Secret, or taken verbatim from `platform.postgres.connectionString`.

Storage keys moved out of `gallery-secrets` entirely: the API/worker read
`Storage__AccessKey`/`Storage__SecretKey` from the operator-created `gallery-s3`
(`accessKey`/`secretKey` keys).

Verified in the render: both Secrets carry the same generated password, the DSN
host follows a custom `platform.postgres.name`, `gallery-secrets` has exactly
four keys, and an explicit `connectionString` is used verbatim. The lookup path
cannot be exercised without a cluster; it is the standard Helm pattern and is
called out in ticket 06.

