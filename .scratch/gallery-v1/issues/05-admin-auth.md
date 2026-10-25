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

## Comments

Implemented with the JwtBearer handler reading the token **from the cookie** (`OnMessageReceived`), rather than the cookie handler: the token stays a JWT (revocable by rotating the signing key) while the browser keeps it out of JavaScript's reach.

Notes:

- **Login always runs both checks**, and compares the username in constant time, so a wrong username and a wrong password are indistinguishable in both response and cost.
- **The CSRF guard is an endpoint filter on the whole `/admin` group**: mutations need `X-Requested-With: photo-gallery`, and any `Origin` present must match `Cors:AllowedOrigin`. A cross-origin HTML form can satisfy neither.
- **`SameSite=None` requires `Secure`**, which breaks plain-HTTP local development, so `Auth:CookieSecure` defaults to true and `appsettings.Development.json` sets it false.
- **`Auth:SigningKey` missing** does not fail startup — the OpenAPI generator runs this entry point — but readiness already fails, so an unconfigured pod never serves traffic.
- **`dotnet run -- hash-password '<password>'`** produces the PBKDF2 hash; documented in `backend/README.md`.
- Password hashing is PBKDF2-SHA256 at 210k iterations, encoded with its parameters so the count can be raised later without a migration.

Verified: 7 auth tests covering cookie flags, wrong password, unauthenticated access, a valid session, a missing custom header, a foreign origin, and the 5/minute rate limit.
