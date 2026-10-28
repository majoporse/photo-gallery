# Backend

ASP.NET Core 10 MVC controllers, plus a worker that only renders. Seven projects
in three groups; `hosts` may reference anything, `core` may not reference
`platform` or `hosts`, and `platform` implements ports declared in `core`.

```
src/core/Gallery.Domain          entities and invariants, no packages but Ulid
src/core/Gallery.Contracts       the two queues, the wire messages, storage and
                                 imaging ports, the upload policy
src/core/Gallery.Application     API use cases: admin, public gallery, upload requests
src/platform/Gallery.Persistence EF Core, configurations, migrations
src/platform/Gallery.Infrastructure  S3, Magick.NET, FileSignatures, Wolverine
src/hosts/Gallery.Api            controllers, auth, configuration, the only writer
src/hosts/Gallery.Worker         queue in, bytes out; no DbContext, no migrations
```

The API owns every row. The worker reads the private bucket, renders the ladder
and answers with `PhotoProcessed` or `UploadRejected`; the API turns that answer
into the Photo and the terminal status. See `docs/adr/0007` and `0008`.

## Run

```bash
dotnet run --project src/hosts/Gallery.Api
```

The worker needs the same Postgres and bucket, no admin settings, and its own
launch profile supplies them:

```bash
dotnet run --project src/hosts/Gallery.Worker
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
dotnet run --project src/hosts/Gallery.Api -- hash-password 'your password'
```

Paste the output into `Admin__PasswordHash`. Nothing ever stores or logs the plaintext.

## Migrations

`dotnet-ef` is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/platform/Gallery.Persistence -s src/hosts/Gallery.Api
```

The design-time factory reads `GALLERY_CONNECTION` and otherwise falls back to localhost, so generating a migration needs no running API.

## Tests

```bash
dotnet test
```

Integration tests start real containers (Postgres 18, SeaweedFS) via Testcontainers, so Docker must be running. That is the point: the S3 and SQL behaviour under test is the real thing, not a mock.

## Contract

`dotnet build` writes `contracts/openapi.json`. Commit it; CI fails when it drifts from the code. The frontend generates its client from that file, so a stale contract is a broken frontend.
