# Photo Gallery — web

Server-rendered React app for the public gallery and the admin shell. React
Router 8 framework mode (`ssr: true`) runs a small Node service; the .NET API
stays behind it on the cluster network.

## Stack

| Package | Version |
|---|---|
| React / React DOM | 19.3.1 |
| React Router / `@react-router/dev` / `@react-router/node` / `@react-router/serve` | 8.5.0 |
| Vite | 8.3.5 |
| TypeScript | 6.0.3 (see “TypeScript version” below) |
| TanStack Query | 5.104.5 |
| dnd-kit / `@dnd-kit/sortable` / `@dnd-kit/utilities` | 6.3.1 / 10.0.0 / 3.2.2 |
| Tailwind CSS / `@tailwindcss/vite` | 4.3.3 |
| Motion (`motion/react`) | 14.6.0 |
| Orval | 8.45.0 |
| Vitest | 5.0.5 |
| ESLint / typescript-eslint | 10.13.0 / 8.72.0 |

Versions were resolved from the npm registry (`npm view <pkg> version`) on
2026-10-25; they are pinned exactly in `package.json`, and `package-lock.json`
is committed.

## Scripts

| Script | What it does |
|---|---|
| `npm run dev` | Generates the API client, then starts the Vite dev server with SSR and HMR. |
| `npm run build` | `prebuild` regenerates the client; then builds `build/client` and `build/server`. |
| `npm start` | `react-router-serve ./build/server/index.js`. Listens on `PORT` (default 3000). |
| `npm run typecheck` | React Router typegen + `tsc --noEmit`. |
| `npm run lint` | ESLint flat config, zero warnings allowed. |
| `npm test` | Vitest run (node environment; no snapshots). |
| `npm run api:generate` | Orval only; writes `app/api/generated/`. |
| `postinstall` | Runs `api:generate` so a fresh `npm ci` leaves a typecheckable tree. |

## Environment

All values are read at **runtime**, not baked into the bundle at build time.

Defaults live in code (`app/lib/env.server.ts`), so the app runs with no `.env`
at all. To override, copy `.env.example` to `.env`: Vite loads it and copies
every key into `process.env` for the SSR side (it only exposes `VITE_*` to the
browser), without overwriting variables the shell or container already set.

| Variable | Used by | Default | Purpose |
|---|---|---|---|
| `API_INTERNAL_URL` | server loaders / Orval mutator on the server | `http://localhost:8080` | Origin the Node process uses to reach the API over cluster DNS. |
| `PUBLIC_API_URL` | browser hooks / Orval mutator in the browser | derived, see below | Public API origin for admin/client-side calls. |
| `PUBLIC_SITE_URL` | canonical and `og:url` values | `http://localhost:3000` | Public origin of this site. |
| `IMAGE_BASE_URL` | derivative URLs, `og:image` | `http://localhost:8333` | Base URL of the public image bucket. No host is hardcoded in components. |
| `FIXTURES` | loaders | unset | `FIXTURES=1` serves `app/dev/fixtures.ts` instead of calling the API. |
| `PORT` | `react-router-serve` | `3000` | Matches the chart's `containerPort: 3000`. |

The Helm chart provides `API_INTERNAL_URL`, `PUBLIC_SITE_URL` and
`IMAGE_BASE_URL` but not `PUBLIC_API_URL`. When it is absent, this app derives
`https://api.<site-host>` from `PUBLIC_SITE_URL`, which matches the three-host
layout in the spec. Locally it falls back to `http://localhost:8080`.

## Rendering and data contract

Public routes use **server `loader`s**:

- `/` — `GET /api/v1/home` and `GET /api/v1/intro?count=10` (the scroll-driven
  opening sequence; it falls back to the featured photographs if the intro call
  fails)
- `/collections` — `GET /api/v1/collections`
- `/collections/:slug` — `GET /api/v1/collections/{slug}` and
  `/api/v1/collections/{slug}/photos?page=N`
- `/photos/:id` — `GET /api/v1/photos/{id}`

Each loader creates its own `QueryClient` (per request), fetches through the
Orval-generated query options, and returns `dehydrate(queryClient)`. The route
component wraps its tree in `HydrationBoundary`, so the generated hooks read the
server data from cache instead of refetching immediately. Public queries use
`staleTime: 60_000`; in fixtures mode it is `Infinity` so the browser never
tries to refetch from an API that is not running.

`/admin` is a client-only area and is marked `noindex, nofollow`. The layout's
`clientLoader` checks `GET /admin/session` once; a 401 on a deep link redirects
to `/admin?returnTo=…` and sign-in returns to that route. These screens all run
their TanStack Query calls in the browser, so a logged-out document request
contains only the `HydrateFallback`:

| Route | Screen |
|---|---|
| `/admin` | login |
| `/admin/collections` | list, create, rename, hide, drag to reorder |
| `/admin/collections/:slug` | photo grid, upload, reorder, feature/rank |
| `/admin/photos/:id` | title, caption, alt text, taken at, collection, featured rank, hide |

Every admin request sends the session cookie (`credentials: "include"`); every
mutation also sends `X-Requested-With: photo-gallery` because the ASP.NET guard
rejects it otherwise. Uploads use `XMLHttpRequest` instead of `fetch` so each
file has real progress, and they are sent one at a time to avoid racing the
API's next-position calculation. On any 401 the admin query cache redirects to
login with the current path as `returnTo`.

`/about` renders the editable copy in `app/content/site.ts`, and `/healthz` is a resource route.

Hydration is deliberately not a client-side API call: on a document request the
Node process talks to the API; on client-side navigation React Router fetches
the loader data from the Node process. Only the admin area talks to the API from
the browser, using `PUBLIC_API_URL`.

Image URLs are always built as:

```
${IMAGE_BASE_URL}/photos/derivatives/{sha256}/{width}w.{avif|webp|jpg}
```

using `availableWidths` from the API. `pickWidth` never upscales: `og:image`
uses the largest available width at or below 1280. Ticket 08 owns the gallery
grid, carousel and lightbox; this ticket renders dominant-colour placeholder
blocks plus real titles and dimensions.

## `/healthz` contract

`GET /healthz` returns `200`, `text/plain`, body `ok`, with `Cache-Control:
no-store`. It is a resource route with no API or database access, so it stays
green while the API is down. The chart's liveness and readiness probes point at
it.

## Themes

Three palettes are selected by `data-theme` on `<html>`: `paper`, `ink`, `grey`.
Tokens live in `app/app.css` as `--bg`, `--fg`, `--muted`, `--rule`, `--accent`
(plus `--surface` and `--accent-fg`). Radius tokens are all `0rem`; borders are
1px rules, not cards or shadows.

The server reads the `theme` cookie to render the correct `data-theme`
immediately. A small inline script in `<head>` (before the stylesheet) re-reads
the cookie as a fallback, so there is no flash even if HTML is cached. The
toggle is mount-gated through `useSyncExternalStore`, writes the cookie for one
year, and is the only client-only part of the chrome.

## Fixtures mode

```sh
FIXTURES=1 npm run dev
# or a production build
npm run build
FIXTURES=1 npm start
```

Loaders read `app/dev/fixtures.ts` instead of touching the network. Fixtures
cover three collections, nine photographs, home featured entries, detail EXIF,
and prev/next links. Unknown slugs and photo ids still produce a real 404.
Images are represented by dominant-colour blocks, so the app is usable with no
backend and no image host. Example id:

```
01JQZ5F8S4M2X9K7C3V1B6N0A1
```

`FIXTURES` is read at request time; it does not need to be present during
`npm run build`.

## API client (Orval)

`orval.config.ts` reads `../contracts/openapi.json` and writes a TanStack Query
client to `app/api/generated/` (gitignored). The generated functions call
`apiFetch` from `app/api/http.ts`, which:

- uses `API_INTERNAL_URL` when `window` is undefined (server);
- uses `window.__PUBLIC_API_URL__` in the browser, injected by the root loader;
- throws `ApiError` with the HTTP status, which loaders translate into
  `throw new Response(null, { status: 404 })` for missing collections/photos.

Regenerate after any contract change:

```sh
npm run api:generate
```

`prebuild`, `dev` and `postinstall` all run it. Do not edit generated files; they
are deleted on every generation.

## TypeScript version

The spec asks for TypeScript 7 with TypeScript 6 aliased for typescript-eslint.
typescript-eslint 8.72 still declares `typescript >=4.8.4 <6.1.0`, so this app
uses **TypeScript 6.0.3** for `tsc` and for lint. A compiler that cannot run
lint is worse than a slightly older compiler; the two-compiler alias can be
revisited when typescript-eslint supports 7. `tsconfig.json` is strict and sets
`noUncheckedIndexedAccess`, `verbatimModuleSyntax`, `moduleResolution:
"bundler"` and `noEmit`.

## Tests

```sh
npm test
```

Covered without snapshots: theme cookie parsing and fallback, derivative URL
construction, width selection/srcset, fixture shape/invariants (unique ids,
canonical widths, hex colours, prev/next links, 404 cases), and admin helpers
(slug derivation and validation, upload-rejection wording, the full PATCH photo
payload, `datetime-local` conversions and collection ordering).

## End-to-end tests

```bash
docker compose up -d          # from the repository root
npm run test:e2e
```

Playwright covers the visitor path (grid, lightbox keyboard and focus, photo metadata), the owner path (sign in, create a collection, upload a valid and an invalid file, confirm persistence) and the edges (logged-out admin redirect, 404, sitemap). Each run provisions its own collection through the admin API and soft-deletes it afterwards, so it does not depend on demo content.

The suite signs in several times a minute from one IP, which is why Compose sets `Auth__LoginPermitLimit=50`; production keeps the default of 5.
