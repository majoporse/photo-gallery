# Backend

ASP.NET Core 10 MVC controllers. Four projects; dependencies point inward only.

```
src/Gallery.Domain          entities and invariants, no packages but Ulid
src/Gallery.Application     use cases, DTOs, storage and query interfaces
src/Gallery.Infrastructure  EF Core, S3, Magick.NET
src/Gallery.Api             controllers, auth, configuration
```

## Run

```bash
dotnet run --project src/Gallery.Api
```

`dotnet run` uses the `Gallery.Api` launch profile, which carries working
development defaults: the local connection string, the SeaweedFS endpoint and
dev credentials, a development admin (`admin` / `dev-password`), a signing key
and `http://localhost:3000` as the allowed origin. Start the storage half with
`docker compose -f compose.storage.yaml up -d` from the repository root and it
works with no environment variables at all.

Without that profile — a container, or a production host — the process starts
and `/api/v1/health` answers, but `/api/v1/health/ready` returns 503 and names
the missing settings. That is deliberate: a pod with an incomplete
configuration never goes Ready.

## Configuration

Environment variables use `__` for nesting.

| Key | Meaning |
| --- | --- |
| `ConnectionStrings__Gallery` | Npgsql connection string |
| `Storage__Endpoint` | S3 endpoint, e.g. `http://seaweed-s3:8333` |
| `Storage__PublicBucket` / `Storage__PrivateBucket` | `photos` / `originals` |
| `Storage__AccessKey` / `Storage__SecretKey` | S3 credentials |
| `Admin__Username` | the single admin |
| `Admin__PasswordHash` | PBKDF2 hash, see below |
| `Auth__SigningKey` | HS256 key, 32+ bytes |
| `Cors__AllowedOrigin` | the SPA origin, e.g. `https://gallery.example.com` |

`example.json` next to this README lists every key with its development default.
It is a reference only: nothing loads it, and the credentials in it are the same
committed `dev-password` hash as `.env.example`.

## Admin password hash

```bash
dotnet run --project src/Gallery.Api -- hash-password 'your password'
```

Paste the output into `Admin__PasswordHash`. Nothing ever stores or logs the plaintext.

## Migrations

`dotnet-ef` is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/Gallery.Infrastructure -s src/Gallery.Api -o Persistence/Migrations
```

The design-time factory reads `GALLERY_CONNECTION` and otherwise falls back to localhost, so generating a migration needs no running API.

## Tests

```bash
dotnet test
```

Integration tests start real containers (Postgres 18, SeaweedFS) via Testcontainers, so Docker must be running. That is the point: the S3 and SQL behaviour under test is the real thing, not a mock.

## Contract

`dotnet build` writes `contracts/openapi.json`. Commit it; CI fails when it drifts from the code. The frontend generates its client from that file, so a stale contract is a broken frontend.
