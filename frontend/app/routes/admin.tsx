import { useState } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { useLoaderData } from "react-router";
import { Button } from "~/components/ui/button";
import { makeQueryClient } from "~/lib/query";

import type { Route } from "./+types/admin";

export const meta: Route.MetaFunction = () => [
  { title: "Admin — Gallery" },
  { name: "robots", content: "noindex, nofollow" },
];

export function clientLoader() {
  return { authenticated: false };
}

clientLoader.hydrate = true as const;

export function HydrateFallback() {
  return (
    <section className="mx-auto max-w-md px-5 py-24">
      <p className="text-muted-foreground">Loading admin…</p>
    </section>
  );
}

export default function Admin() {
  const { authenticated } = useLoaderData<typeof clientLoader>();
  const [queryClient] = useState(() => makeQueryClient(0));

  return (
    <QueryClientProvider client={queryClient}>
      <section className="mx-auto max-w-md px-5 py-20">
        <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
          Admin
        </p>
        <h1 className="mt-3 font-heading text-4xl tracking-tight">Sign in</h1>
        <p className="mt-4 text-sm text-muted-foreground">
          The real sign-in form and collection management arrive in ticket 09.
          This page is a client-only route so the public site can stay entirely
          server-rendered.
        </p>

        <form className="mt-10 space-y-4" aria-describedby="login-placeholder">
          <div>
            <label htmlFor="username" className="text-sm">
              Username
            </label>
            <input
              id="username"
              name="username"
              autoComplete="username"
              disabled
              className="mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm disabled:opacity-60"
            />
          </div>
          <div>
            <label htmlFor="password" className="text-sm">
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              disabled
              className="mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm disabled:opacity-60"
            />
          </div>
          <Button type="submit" disabled className="w-full">
            Sign in
          </Button>
          <p
            id="login-placeholder"
            className="text-xs text-muted-foreground"
            aria-live="polite"
          >
            {authenticated
              ? "A session is already active."
              : "Not implemented yet. Nothing is sent anywhere."}
          </p>
        </form>
      </section>
    </QueryClientProvider>
  );
}
