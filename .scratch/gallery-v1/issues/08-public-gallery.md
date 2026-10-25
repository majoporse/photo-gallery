# 08 — Public gallery: browsing, carousel, lightbox

Status: ready-for-agent
Blocked by: 07

## Context
The part visitors actually see. The bar is: photographs load fast, nothing shifts, and keyboard users can drive the lightbox.

## Scope
- `/` — hero, featured carousel (Embla), collection links; content from the `/home` loader.
- `/collections` — grid of collections with cover and count.
- `/collections/:slug` — paginated grid; click opens the lightbox at that photo.
- `/photos/:id` — standalone page with prev/next, caption and EXIF; `og:image` is the 1280 derivative; JSON-LD `ImageObject`.
- `<picture>` with AVIF (if the ladder has it) / WebP / JPEG sources and explicit `width`/`height`; `loading="lazy"` below the fold, eager for the first image; `sizes` matching the grid.
- Dominant-colour background behind every image so the box is never empty and CLS stays at zero.
- Lightbox: yet-another-react-lightbox with zoom, captions, thumbnails; opened via a plain link to `/photos/:id` that is intercepted client-side, so middle-click and crawlers still work.
- `robots.txt` and a generated `sitemap.xml` listing collections and photos.
- 404 route for unknown slug/id, server-rendered with a real 404 status.

## Done when
- View-source on a collection page contains photo markup and absolute image URLs.
- The social preview for a photo page resolves (check with a card validator or by inspecting the tags).
- CLS is 0 on a collection page in Lighthouse, and no carousel item renders at zero height before hydration.
- The lightbox is fully operable from the keyboard and returns focus to the triggering tile on close.
- A deep link to `/photos/:id` followed by "back" restores the grid scroll position.
