# Backend

ASP.NET Core 10 MVC controllers, plus a worker that only renders. Two services,
one shared contract project, one shared policy package.

```
src/gallery/Gallery.Domain            entities and invariants, no packages but Ulid
src/gallery/Gallery.Application       API use cases: admin, public gallery, upload requests,
                                      the object-store port
src/gallery/Gallery.Infrastructure    EF Core, migrations, queries, S3, Wolverine
src/gallery/Gallery.Api               controllers, auth, configuration, the only writer

src/worker/Gallery.Worker.Application       the handler, the pipeline, its own ports and policy use
src/worker/Gallery.Worker.Infrastructure    S3 and Magick.NET, its own copies
src/worker/Gallery.Worker                   the host: queue in, bytes out

src/notifier/Gallery.Notifier         the SSE endpoint and the status fan-out

src/contracts/Gallery.Contracts       the wire messages and queue names, nothing else
src/common/Gallery.Policy             the upload limits and the derivative ladder
```

The API owns every row. The worker reads the private bucket, renders the ladder
and answers with `PhotoProcessed` or `UploadRejected`; the API turns that answer
into the Photo and the terminal status, then publishes an `UploadView` on the
status queue. The notifier consumes it and pushes it to the admin's open SSE
connections. See `docs/adr/0007`, `0008` and `0010`.

The worker duplicates its object-store adapter and ports on purpose: it is meant
to be replaceable, including by a non-.NET implementation, so it shares messages
and policy with the gallery and no assemblies. `docs/adr/0009` records what still
stands in the way of that.

## Run

```bash
dotnet run --project src/gallery/Gallery.Api
```

The worker needs the same Postgres and bucket, no admin settings, and its own
launch profile supplies them:

```bash
dotnet run --project src/worker/Gallery.Worker
```

The notifier needs the signing key and the queue, not the admin password. Its
launch profile supplies both, on `http://localhost:8081`:

```bash
dotnet run --project src/notifier/Gallery.Notifier
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
dotnet run --project src/gallery/Gallery.Api -- hash-password 'your password'
```

Paste the output into `Admin__PasswordHash`. Nothing ever stores or logs the plaintext.

## Migrations

`dotnet-ef` is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/gallery/Gallery.Infrastructure -s src/gallery/Gallery.Api -o Persistence/Migrations
```

The design-time factory reads `GALLERY_CONNECTION` and otherwise falls back to localhost, so generating a migration needs no running API.

## Tests

```bash
dotnet test
```

Integration tests start real containers (Postgres 18, SeaweedFS) via Testcontainers, so Docker must be running. That is the point: the S3 and SQL behaviour under test is the real thing, not a mock.

## Contract

`dotnet build` writes `contracts/openapi.json`. Commit it; CI fails when it drifts from the code. The frontend generates its client from that file, so a stale contract is a broken frontend.
