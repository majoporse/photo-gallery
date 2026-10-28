# Direct upload with asynchronous processing

The admin's browser uploads originals straight to object storage with a presigned URL, then tells the API the bytes have landed. A separate worker validates and renders the derivative ladder, and pushes status changes to the admin over server-sent events. See ADR 0006 and ADR 0007.

## Flow

1. `POST /api/v1/admin/collections/{id}/uploads` with `{ fileName, bytes }`. The API checks the collection exists, the declared size is within 50 MB, creates an **Upload** row in `Pending`, reserves its `Position` from the collection's counter, and returns `{ uploadId, key, url, expiresAt, position }` where `url` is a presigned `PUT` valid for 15 minutes.
2. The browser `PUT`s the file to `url`. Progress comes from `XMLHttpRequest.upload`.
3. `POST /api/v1/admin/uploads/{id}/complete` with no body. The API confirms the object exists, moves the Upload to `Queued`, and publishes `ProcessUpload` in the same transaction. Returns `202` with the Upload view.
4. The worker moves it to `Processing`, downloads the original, validates, hashes, renders the ladder, inserts the Photo using the reserved position, and marks the Upload `Succeeded` with the `PhotoId`.
5. `GET /api/v1/admin/uploads/events` streams `text/event-stream`. The first event is `snapshot` (all non-terminal uploads), then `upload.updated` on every change.

## Upload states

`Pending` → `Uploaded` → `Queued` → `Processing` → `Succeeded` | `Rejected` | `Expired`.

Only `Succeeded` produces a Photo. `Rejected` carries a rejection code; validation rejections delete the original, `ProcessingFailed` keeps it. `Pending` and `Uploaded` rows older than 24 hours are expired by the sweeper and their objects deleted.

## Rules

- Nothing enters `Photos` until validation passes.
- Positions follow upload order, not completion order.
- A presigned URL is only minted for an authenticated admin, points at one key, and expires.
- Validation remains server-side and authoritative; magic bytes beat both extension and declared content type.
- Uploads are processed at least once; processing a second time must not create a second Photo.
