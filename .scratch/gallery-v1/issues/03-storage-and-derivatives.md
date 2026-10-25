# 03 — Object storage and the derivative pipeline

Status: ready-for-agent
Blocked by: 01

## Context
Upload is the only write that touches bytes, and it is where the whole image experience is decided. Do this before the endpoints so the pipeline can be tested against real files.

**Start with a 30-minute spike: AVIF encoding.** SkiaSharp exposes JPEG, PNG and WebP encoders but no AVIF encoder. Confirm whether Magick.NET (Apache-2.0) in this environment writes AVIF, or accept WebP + JPEG for v1 and drop AVIF from the ladder. Do not ship a ladder that silently falls back. Record the outcome on this ticket before implementing.

## Scope
- `IObjectStore` and `IImageProcessor` in `Application`; implementations in `Infrastructure`.
- `AWSSDK.S3` against a custom endpoint with path-style addressing and two buckets (`photos`, `originals`).
- Pipeline: sniff magic bytes → SHA-256 → stream the untouched original to `originals/{photoId}/{name}` → decode with a bounded pixel budget → write each ladder width and format to `photos/derivatives/{sha256}/{width}w.{ext}` with `Content-Type` and `Cache-Control: public, max-age=31536000, immutable` → extract EXIF (MetadataExtractor) and dominant colour → return a `ProcessedPhoto` value.
- Rejections: > 50 MB, extension/magic mismatch, long edge < 640 px, decode failure. Each rejection is a typed result, not an exception.
- Idempotence: re-processing identical bytes must not create duplicate objects or change keys.
- Concurrency bounded so eight simultaneous 2048px encodes cannot exhaust memory.

## Done when
- An integration test uploads a JPEG and a PNG against a SeaweedFS container and finds the original plus every ladder object at the expected keys.
- The same file twice produces identical keys and no duplicate writes.
- A file with a `.jpg` extension and PNG bytes is rejected with a typed reason.
- Cache headers round-trip on GET (this also settles the open question in the spec).

## Comments

Spike result: **SkiaSharp 4.153.1 lists `SKEncodedImageFormat.Avif` but `Encode` returns null**, so it cannot produce AVIF at all. Magick.NET 15.0.0 encodes it (verified with a 32×32 test: 291 bytes). The ladder therefore uses Magick.NET for decode, auto-orient, resize, encode and EXIF, and SkiaSharp is not a dependency.

Implementation notes:

- Ladder is 320/640/1280/2048 × AVIF/WebP/JPEG, skipping widths larger than the original rather than upscaling. A 1600px upload yields 9 objects, not 12.
- Uploads are read into memory under a 50 MB cap; two encodes run at a time so eight concurrent 2048px jobs cannot exhaust memory.
- Derivatives are stripped and JPEGs are flattened onto white, so the ladder never carries GPS even though the API publishes EXIF as JSON.
- `Cache-Control` round-trips through SeaweedFS object metadata — asserted with a real `HeadObject`, which closes the spec's open question.

Verified against a real `weed mini` container: original plus ladder stored, identical bytes producing identical keys with no new objects, PNG-named-JPEG rejected, tiny images rejected, oversized declared length rejected before reading.
