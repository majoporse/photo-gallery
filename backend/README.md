# Backend

ASP.NET Core 10 minimal API. Four projects; dependencies point inward only.

```
src/Gallery.Domain          entities and invariants, no packages but Ulid
src/Gallery.Application     use cases, DTOs, storage and query interfaces
src/Gallery.Infrastructure  EF Core, S3, Magick.NET
src/Gallery.Api             endpoints, auth, configuration
```

## Run

```bash
dotnet run --project src/Gallery.Api
```

Without configuration the process starts and `/api/v1/health` answers, but `/api/v1/health/ready` returns 503 and names the missing settings. That is deliberate: a pod with an incomplete configuration never goes Ready.

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
