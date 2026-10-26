import { useState } from "react";
import {
  Links,
  Meta,
  Outlet,
  Scripts,
  ScrollRestoration,
  useLocation,
  useRouteLoaderData,
} from "react-router";
import { QueryClientProvider } from "@tanstack/react-query";

import type { Route } from "./+types/root";
import "./app.css";
import { RouteError } from "~/components/route-error";
import { SiteFooter } from "~/components/site-footer";
import { SiteHeader } from "~/components/site-header";
import {
  fixturesEnabled,
  getImageBaseUrl,
  getPublicApiUrl,
  getSiteUrl,
} from "~/lib/env.server";
import { makeQueryClient, PUBLIC_STALE_TIME } from "~/lib/query";
import { RootDataProvider, useRootData, type RootData } from "~/lib/root-data";
import {
  DEFAULT_THEME,
  THEME_INIT_SCRIPT,
  themeFromCookieHeader,
} from "~/lib/theme";

export function loader({ request }: Route.LoaderArgs) {
  return {
    theme: themeFromCookieHeader(request.headers.get("cookie")),
    fixtures: fixturesEnabled(),
    publicApiUrl: getPublicApiUrl(),
    imageBaseUrl: getImageBaseUrl(),
    siteUrl: getSiteUrl(),
  };
}

export const meta: Route.MetaFunction = () => [
  { title: "Gallery — a personal photography archive" },
  {
    name: "description",
    content:
      "A small, quiet archive of photographs, arranged into collections.",
  },
  { property: "og:site_name", content: "Gallery" },
  { property: "og:type", content: "website" },
  { name: "robots", content: "index, follow" },
];

const FALLBACK_ROOT_DATA: RootData = {
  theme: DEFAULT_THEME,
  fixtures: false,
  publicApiUrl: "",
  imageBaseUrl: "",
  siteUrl: "",
};

export function Layout({ children }: { children: React.ReactNode }) {
  const data = useRouteLoaderData<typeof loader>("root");
  const rootData: RootData = data ?? FALLBACK_ROOT_DATA;
  const location = useLocation();
  const intro = location.pathname === "/" ? "playing" : undefined;

  return (
    <html lang="en" data-theme={rootData.theme} data-intro={intro}>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
        <script dangerouslySetInnerHTML={{ __html: THEME_INIT_SCRIPT }} />
        <script
          dangerouslySetInnerHTML={{
            __html: `window.__PUBLIC_API_URL__=${JSON.stringify(
              rootData.publicApiUrl,
            ).replaceAll("<", "\\u003c")};`,
          }}
        />
        <Meta />
        <Links />
        <noscript
          dangerouslySetInnerHTML={{
            __html:
              "<style>header[data-site-header]{position:static!important;visibility:visible!important;transform:none!important;transition:none!important}</style>",
          }}
        />
      </head>
      <body>
        <RootDataProvider value={rootData}>
          <a
            href="#main"
            className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:border focus:border-rule focus:bg-bg focus:px-3 focus:py-2"
          >
            Skip to content
          </a>
          <div className="flex min-h-dvh flex-col">
            <SiteHeader />
            <main id="main" className="flex-1">
              {children}
            </main>
            <SiteFooter />
          </div>
        </RootDataProvider>
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export default function App() {
  const { fixtures } = useRootData();
  const [queryClient] = useState(() =>
    makeQueryClient(fixtures ? Number.POSITIVE_INFINITY : PUBLIC_STALE_TIME),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <Outlet />
    </QueryClientProvider>
  );
}

export function ErrorBoundary() {
  return <RouteError />;
}
