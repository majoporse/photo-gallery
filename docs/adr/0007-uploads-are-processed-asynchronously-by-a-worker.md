# Uploads are processed asynchronously by a separate worker

Rendering the derivative ladder takes seconds per photograph and is CPU-bound. Doing it inside the request that enqueued it made bulk uploads a sequence of long waits and tied processing capacity to the API's replica count. Processing now happens in a `Gallery.Worker` deployment that consumes from a Postgres-backed queue, and the admin watches progress over server-sent events.

**Consequences.** The `Uploads` table is the source of truth for what is happening; the queue only delivers the instruction. Wolverine's Postgres transport (its own `wolverine_queues` schema) is used rather than a broker, because the enqueue must be transactional with the status change and a second stateful service is not worth its operational cost at this volume. That schema is created by `AutoProvision()` and therefore does **not** appear in the EF Core migrations — a second schema-management step, accepted knowingly.

Delivery is at-least-once, so the handler is idempotent: a retry that finds the Photo already inserted marks the Upload succeeded instead of inserting again. Two failure channels are kept apart — validation rejections (`TooSmall`, `CorruptImage`, …) are final and delete the bytes; infrastructure failures retry three times with backoff and then land in `Rejected` with `ProcessingFailed`, keeping the original so nothing of the owner's is destroyed.

Publishing does not wait for a poll. Wolverine's `EnableQueueNotifications` makes the sender emit a `NOTIFY` and the worker's listener wake on it, with polling left as a recovery path; the setting is on in both processes because the sender is what emits the notification.

A sweeper in the worker repairs what the queue cannot see, on two cadences: every 30 seconds it re-publishes work whose message was lost or whose worker died mid-`Processing`, and hourly it expires uploads whose bytes never arrived and purges old terminal rows. It is deliberately *not* the trigger for normal processing.

Positions are reserved when the upload is created, from a per-collection counter, so the gallery order is the owner's upload order no matter how many workers run. A rejected upload leaves a gap, which nothing reads. A sweeper in the worker expires uploads whose bytes never arrived, reclaims jobs abandoned mid-`Processing`, and purges old terminal rows.

Status changes reach the browser through `LISTEN/NOTIFY`: the writer notifies on commit, each API replica relays to its own event streams. A notification lost to a crash is recovered by the stream's opening snapshot on reconnect, not by an event log.

**Rejected.** A `BackgroundService` inside the API — processing competes with request serving, a restart loses in-flight jobs, and capacity cannot scale independently. Hangfire — its enqueue writes on its own connection, so "mark queued" and "enqueue" can diverge, which is the exact failure the `Uploads` table exists to prevent. MassTransit — its SQL transport fits, but version 9 is commercially licensed. A broker (RabbitMQ, Redis) — another stateful service to run for tens of jobs a day.
