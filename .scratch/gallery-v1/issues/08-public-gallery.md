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

## Comments

Implemented. Verified independently of the agent's report: lint clean, `tsc --noEmit` clean, 35 Vitest tests, production build, and against the real Compose stack — SSR HTML contains `<picture>` with AVIF/WebP/JPEG `srcset` at the widths the API reports, `og:image` points at the 1280 JPEG (which returns `200 image/jpeg`), the sitemap lists collections and photos with absolute URLs, and an unknown slug returns a real 404.

Measured: Lighthouse CLS **0** on a collection page, performance 0.94. With JavaScript blocked, carousel slides measure 545px rather than collapsing — the failure mode that makes photo galleries jump.

Lightbox behaviour confirmed in a browser: tile keeps its `/photos/:id` `href`, middle-click opens the real page in a new tab instead of the overlay, arrow keys move slides, Escape closes and returns focus to the triggering tile, and no `yarl__` markup exists in server HTML.

Two deviations:

- **`public/robots.txt` became a runtime route.** A static file shadows the route and would hard-code `localhost` in the `Sitemap:` line. `Disallow: /admin` is preserved.
- **The seed does not set `featuredRank`**, so `/` says "Nothing is featured yet" on a freshly seeded stack. The carousel itself was verified with fixtures. Fixed in the seed rather than in the UI.

Unproven: an external social-card validator (tags were inspected and the image fetched, but no third-party check), and EXIF rendering against real data — seeded images carry no EXIF, so that path is covered by fixtures and unit tests only.
