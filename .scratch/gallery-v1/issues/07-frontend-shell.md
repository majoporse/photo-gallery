# 07 — Frontend shell, theme tokens, generated client

Status: ready-for-agent
Blocked by: 04

## Context
The frame everything else mounts into: SSR runtime, design tokens, data plumbing, and the Orval client that keeps the frontend honest about the contract.

## Scope
- `frontend/` with Vite 8, React 19, React Router 8 framework mode, `ssr: true`, `react-router.config.ts` and `app/routes.ts` per the spec's route table.
- Tailwind 4 + shadcn (`shadcn` CLI, not `shadcn-ui`), radius `0` everywhere, hairline rules instead of cards/shadows.
- Three themes via `data-theme` (`paper`, `ink`, `grey`) over the token names in the spec; persisted in a `theme` cookie; applied by an inline script in `<head>` so there is no flash; toggle is a client-only component.
- TanStack Query 5 with a `QueryClient` created per request on the server, dehydrated into `HydrationBoundary`, `staleTime` 60 s for public queries.
- Orval config generating TanStack Query hooks from `contracts/openapi.json` into `src/api/generated` (gitignored), wired to a `prebuild` script and to `dev`.
- Layout with nav, footer, and an error boundary; `/admin` routes excluded from `noindex`-ing rules that apply to the public site.

## Done when
- `npm run build && node build/server/index.js` serves real HTML at `/collections` (content in view-source, not a shell).
- Toggling the theme survives reload with no flash of the wrong palette.
- `npm run lint` and `tsc --noEmit` pass; the generated client typechecks against a page that consumes one endpoint.
- Navigating client-side from `/` to `/collections` issues no full page load.

## Comments

Built in parallel with the backend by a delegated agent; I re-ran the checks rather than trusting the report: `npm run lint` clean, `npx tsc --noEmit` clean, `npm test` 22 passing, `npm run build` producing `build/server/index.js`.

Deviations and decisions:

- **TypeScript 6.0.3, not 7** — typescript-eslint 8.72 declares `typescript >=4.8.4 <6.1.0`, and a lint setup that cannot run is worse than an older compiler. Recorded in `frontend/README.md`.
- **`app/` layout, not `src/`** — that is React Router framework mode's convention, and it also determines where Orval writes (`app/api/generated`, gitignored).
- **`PUBLIC_API_URL`** is needed for browser calls but the chart only supplies `API_INTERNAL_URL`, `PUBLIC_SITE_URL` and `IMAGE_BASE_URL`. When absent it is derived as `https://api.<site-host>`, so the chart does not need a fourth variable. Documented.
- **Admin is `noindex, nofollow`** plus a `robots.txt` disallow; public routes are `index, follow`.
- **Placeholder cards** render real API data (title, dimensions, dominant colour) but the grid/carousel/lightbox belong to ticket 08.

Verified by the agent against a running server: `/healthz` answers 200 with the API unreachable, `/collections` HTML contains the `<h1>` and cards, a photo page emits an absolute `og:image`, unknown slugs return 404, the theme cookie survives reload with no flash, and a client-side navigation keeps a single document.

Still unproven: real API-backed SSR (no backend was running during the build), actual SeaweedFS image delivery, and the container/Helm probes. Ticket 08 closes the first two.
