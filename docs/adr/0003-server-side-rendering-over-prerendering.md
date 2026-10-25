# Server-side rendering instead of prerendering

The site must be indexable and must produce correct link previews, which rules out a purely client-rendered SPA. Prerendering every route at build time was the first choice, but it welds content to deploys: a new photo only appears after a rebuild, which forces a publish button, a draft state, and an endpoint that exists solely so a build can enumerate routes. We render instead with **React Router 8 framework mode, `ssr: true`**, running a small Node service next to the .NET API.

**Consequences.** Two runtimes in the cluster instead of one static file server — that is the entire cost. Uploading a photo makes it visible immediately, so there is no publish step and no `publishedAt`. Public routes use server `loader`s that call the API over cluster DNS, so the API needs no CORS for reads and the Node layer can cache HTML later. Admin routes use `clientLoader`s, calling the API directly from the browser with the session cookie, with CORS restricted to the frontend origin.

**Rejected.** Prerender/SSG — correct output, wrong operational shape for a site whose content changes by upload. CSR plus server-injected meta tags — no Node, but crawlers receive an empty body and image indexing collapses.
