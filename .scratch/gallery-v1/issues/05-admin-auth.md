# 05 — Admin authentication

Status: ready-for-agent
Blocked by: 01

## Context
One admin, configuration-seeded, session in a cookie. Cross-origin by design: the SPA lives on a different hostname than the API.

## Scope
- `Admin__Username` and `Admin__PasswordHash` (PBKDF2 or Argon2, documented hashing command in the README), `Auth__SigningKey`.
- `POST /api/v1/admin/session` verifies and issues an HS256 JWT (12 h, `sub=admin`) in an `HttpOnly; Secure; SameSite=None; Path=/` cookie named `pg_session`; `DELETE` clears it.
- Authorization policy for `admin` applied to the `/admin` group.
- CORS: single allowed origin from config, credentials allowed, only the headers the client sends.
- CSRF: mutations require `X-Requested-With: photo-gallery` and a matching allowlisted `Origin`; GETs unaffected.
- Rate limit login to 5 attempts/minute per IP.
- Constant-time comparison and identical failure responses for unknown user and wrong password.

## Done when
- Wrong password, missing `X-Requested-With`, and a foreign `Origin` each fail with the documented status.
- A valid cookie reaches an admin-only route; an expired/forged one does not.
- Rotating `Auth__SigningKey` invalidates an existing cookie, proven by test.
- No password or key appears in logs (assert by capturing log output in a test).
