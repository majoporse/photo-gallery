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
