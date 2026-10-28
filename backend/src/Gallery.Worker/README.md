# Gallery.Worker

Renders the derivative ladder for uploads. It is a host, not a library: almost
none of the image work lives in this directory.

## Where the work actually is

| Concern | Location |
| --- | --- |
| Consume the queue, claim a message, retry | `Program.cs` (Wolverine `ListenToPostgresqlQueue`) + `Gallery.Infrastructure/Uploads/ProcessUploadHandler.cs` |
| Validate, hash, encode the ladder, extract EXIF | `Gallery.Application/Storage/UploadPipeline.cs` |
| Download the original, insert the Photo, notify | `Gallery.Infrastructure/Uploads/ProcessUploadHandler.cs` |
| Repair and housekeeping | `UploadSweeper.cs` |

The handler sits in **Infrastructure** rather than here because it depends only
on services that already live there, and the integration tests invoke it
directly without starting a queue. `Program.cs` discovers it with
`IncludeAssembly(typeof(ProcessUploadHandler).Assembly)`.

## What a message does

1. Load the `Uploads` row; return if it is already terminal (at-least-once
   delivery means a duplicate is normal).
2. Mark it `Processing` and `NOTIFY` the admin stream.
3. Ask the bucket for the object's length and refuse it over 50 MB without
   downloading it.
4. Download it, validate magic bytes, dimensions and pixel count, hash it, and
   render 320/640/1280/2048 in AVIF, WebP and JPEG.
5. Lock the collection row, insert the `Photos` row at the position reserved
   when the upload was created, mark the Upload `Succeeded`, commit.
6. Validation failures delete the original and become `Rejected`; infrastructure
   failures back off three times and then become `Rejected` with
   `ProcessingFailed`, keeping the original.

## What it does not do

`UploadSweeper` is not the processing trigger. It re-publishes work whose message
was lost and expires abandoned uploads, on its own timers. Normal processing is
driven by the queue notification.
