# Upload ownership

Status: accepted. Decision recorded in `docs/adr/0008-api-is-the-only-database-writer.md`.

## Goal

`Uploads`, `Photos` and `Collections` have exactly one writer: the API. The worker renders bytes and answers with a message. Neither deployment holds the other's schema.

## Decisions

- The worker consumes `ProcessUpload` and answers with `PhotoProcessed` or `UploadRejected`. It never reads or writes the `public` schema.
- `ProcessUpload` is self-contained: `{UploadId, PhotoId, ObjectKey, FileName}`. Those fields are immutable after publish.
- `Processing` is removed as upload status. `Queued` plus a sweeper timeout covers both "not picked up" and "picked up and died".
- Finality has two sources: the worker publishes `UploadRejected(ProcessingFailed, detail)` after Wolverine retries are exhausted, and a sweeper in the API re-publishes stale `Queued` uploads, incrementing `Uploads.Attempts`, rejecting once exhausted.
- The worker deletes the bytes of a rejected upload. A lost reply turns the reason into `MissingBytes`, not the outcome.
- `Gallery.Contracts` does not reference `Gallery.Domain`. The API maps `RejectionReason` to `UploadRejection` explicitly.
- Rendering is worker-only, so demo seeding becomes enqueue-only and `migrate` stops implying `seed`.
- Solution folders: `hosts` may reference anything, `core` may not reference `platform` or `hosts`, `platform` implements ports declared in `core`.

## Non-goals

- No broker. The worker still connects to Postgres for the Wolverine transport; it does not get its own database-free deployment.
- No language portability. The wire format is Wolverine's; a non-.NET worker is not a goal of this change.
- No change to the SSE contract, the presigned upload flow, or the derivative ladder.

## Target layout

```
src/hosts/     Gallery.Api, Gallery.Worker
src/core/      Gallery.Domain, Gallery.Contracts, Gallery.Application
src/platform/  Gallery.Persistence, Gallery.Infrastructure
tests/         Gallery.Api.Tests, Gallery.Integration.Tests
```

Ports (`IObjectStore`, `IImageProcessor`, `IImageFormatDetector`) and the shared upload policy (`MaxBytes`, ladder, formats, key shape, filename sanitising) live in `Gallery.Contracts`. EF lives in `Gallery.Persistence`. S3, Magick, FileSignatures and Wolverine live in `Gallery.Infrastructure`.

## Steps

1. **Contracts project and storage ports.** New `Gallery.Contracts`; move the five storage port files out of `Gallery.Application`; lift `UploadPolicy` out of `UploadPipeline`. No behaviour change.
2. **Split EF out of Infrastructure.** New `Gallery.Persistence` holding `GalleryDbContext`, factory, configurations, migrations, `PhotoPositions`. Query and command implementations stay in `Infrastructure` for now.
3. **Move rendering into the worker.** `ProcessUploadHandler`, `UploadPipeline` and `UploadResult` under `Gallery.Worker/Processing/`; worker registers the pipeline; `AddObjectStorage` stops doing so; seeding becomes enqueue-only (unchanged message shape); `UploadPipeline.ProcessAsync` and its `storeOriginal` branch are deleted.
4. **Reply-based writes.** Widen `ProcessUpload`; add `PhotoProcessed`, `ProcessedPhoto`, `UploadRejected`, `RejectionReason` and `IUploadBus` to `Contracts`; add the `upload-results` queue and the API's `RecordProcessingResult` handler; move `UploadSweeper` to the API; add `Attempts` and its migration; drop `Processing`; worker deletes rejected bytes and reports `MissingBytes`.
5. **Solution folders.** Physical move to `hosts/core/platform`, `Gallery.slnx` folders, `Dockerfile` COPY lines, CI paths, and `Gallery.Infrastructure.Tests` renamed to `Gallery.Integration.Tests`.

## Verification

Each step builds and passes the full suite (Postgres and SeaweedFS via Testcontainers). Step 4 additionally gets a manual run: start the API, begin an upload, stop the worker mid-render, restart it, and watch the sweeper finish or reject the row.
