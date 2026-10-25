import { useCallback, useState } from "react";
import {
  Link,
  NavLink,
  Outlet,
  redirect,
  useLoaderData,
  useNavigate,
} from "react-router";
import { QueryClientProvider, useMutation } from "@tanstack/react-query";
import { adminLogout, adminSession } from "~/api/admin";
import { ApiError } from "~/api/http";
import { Button } from "~/components/ui/button";
import { makeQueryClient } from "~/lib/query";

import type { Route } from "./+types/admin";

export const meta: Route.MetaFunction = () => [
  { title: "Admin — Gallery" },
  { name: "robots", content: "noindex, nofollow" },
];

/**
 * The session check is the only admin loader. Every other admin screen runs
 * its queries in the browser, so a logged-out document request contains no
 * collection or photo data.
 */
export async function clientLoader({ request }: Route.ClientLoaderArgs) {
  const url = new URL(request.url);

  try {
    const session = await adminSession();
    if (url.pathname === "/admin") {
      throw redirect("/admin/collections");
    }
    return { username: session.username };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      if (url.pathname === "/admin") {
        return { username: null };
      }
      const returnTo = `${url.pathname}${url.search}`;
      throw redirect(`/admin?returnTo=${encodeURIComponent(returnTo)}`);
    }
    throw error;
  }
}

clientLoader.hydrate = true as const;

export function HydrateFallback() {
  return (
    <section className="mx-auto max-w-6xl px-5 py-16">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        Admin
      </p>
      <p className="mt-4 text-sm text-muted-foreground">Loading admin…</p>
    </section>
  );
}

export default function AdminLayout() {
  const { username } = useLoaderData<typeof clientLoader>();
  const navigate = useNavigate();

  const onUnauthorized = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        const returnTo = `${window.location.pathname}${window.location.search}`;
        void navigate(`/admin?returnTo=${encodeURIComponent(returnTo)}`, {
          replace: true,
        });
      }
    },
    [navigate],
  );

  const [queryClient] = useState(() =>
    makeQueryClient(0, { retry: 0, onQueryError: onUnauthorized }),
  );

  const logout = useMutation({
    mutationFn: adminLogout,
    onSettled: () => {
      queryClient.clear();
      // A document load discards every cached admin screen and the layout's
      // now-stale session state in one step.
      window.location.assign("/admin");
    },
  });

  return (
    <QueryClientProvider client={queryClient}>
      <section className="mx-auto w-full max-w-6xl px-5 py-10">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-rule pb-4">
          <div className="flex flex-wrap items-baseline gap-5">
            <Link
              to={username ? "/admin/collections" : "/admin"}
              className="font-heading text-xl tracking-tight"
            >
              Admin
            </Link>
            {username ? (
              <nav aria-label="Admin">
                <ul className="flex flex-wrap items-center gap-5">
                  <li>
                    <NavLink
                      to="/admin/collections"
                      className={({ isActive }) =>
                        `border-b pb-0.5 text-sm ${
                          isActive
                            ? "border-accent text-fg"
                            : "border-transparent text-muted-foreground hover:text-fg"
                        }`
                      }
                    >
                      Collections
                    </NavLink>
                  </li>
                  <li>
                    <a
                      href="/"
                      className="border-b border-transparent pb-0.5 text-sm text-muted-foreground hover:border-accent hover:text-fg"
                    >
                      Public site
                    </a>
                  </li>
                </ul>
              </nav>
            ) : null}
          </div>

          {username ? (
            <div className="flex items-center gap-4 text-sm text-muted-foreground">
              <span>{username}</span>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={logout.isPending}
                onClick={() => {
                  logout.mutate();
                }}
              >
                {logout.isPending ? "Signing out…" : "Sign out"}
              </Button>
            </div>
          ) : null}
        </div>

        <div className="pt-8">
          <Outlet />
        </div>
      </section>
    </QueryClientProvider>
  );
}
