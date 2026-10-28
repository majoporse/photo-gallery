# Gallery.Worker

Renders the derivative ladder for uploads. It is a host with its own application
core: `Program.cs` wires the queue, `Processing/` does the work.

## Run

```bash
dotnet run --project src/Gallery.Worker
```

Its launch profile carries the same local defaults as the API's: Postgres and
SeaweedFS on localhost, dev credentials, nothing to configure. Start the storage
half first with `docker compose -f compose.storage.yaml up -d` from the
repository root.

The worker needs a queue, the private bucket, and Postgres only as the queue's
backing store. It has no `DbContext`, no migrations, and no admin or auth
settings: every row belongs to the API.

## Where the work actually is

| Concern | Location |
| --- | --- |
| Consume the queue, claim a message, retry | `Program.cs` (Wolverine `ListenToPostgresqlQueue`) |
| Validate, hash, encode the ladder, extract EXIF | `Processing/UploadPipeline.cs` |
| Download the original, render the ladder, answer with a message | `Processing/ProcessUploadHandler.cs` |

The handler lives here rather than in a shared library because the work is the
worker's: `Program.cs` finds it by convention, and the integration tests invoke
it directly without starting a queue.

## What a message does

1. Ask the bucket for the object's length and refuse it over 50 MB without
   downloading it.
2. Download it, validate magic bytes, dimensions and pixel count, hash it, and
   render 320/640/1280/2048 in AVIF, WebP and JPEG.
3. Publish `PhotoProcessed` with the keys and metadata. The API inserts the Photo
   and marks the Upload succeeded.
4. Validation failures delete the original and publish `UploadRejected`. A missing
   object is reported the same way as `MissingBytes`; infrastructure failures
   retry with a cooldown and then publish `ProcessingFailed` with the exception
   text, so the owner learns why instead of waiting.

Delivery is at-least-once, so a redelivered message re-renders and overwrites
content-addressed keys. It never creates a second Photo, because no Photo is
created here.

## What it does not do

It does not write to the gallery database and does not decide when to give up.
The API's `UploadSweeper` re-publishes uploads that never get an answer, counts
the attempts on the row, and rejects them after `Upload.MaxAttempts`.
