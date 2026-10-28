import { lazy, Suspense } from "react";
import { useMounted } from "~/lib/use-mounted";

import type { Route } from "./+types/api-docs";

const ApiReference = lazy(() => import("~/components/api-reference"));

export const meta: Route.MetaFunction = () => [
  { title: "API reference — Gallery" },
  { name: "robots", content: "noindex, nofollow" },
];

export default function ApiDocs() {
  const mounted = useMounted();

  return (
    <section aria-label="API reference" className="min-h-dvh">
      {mounted ? (
        <Suspense
          fallback={
            <p className="mx-auto max-w-6xl px-5 py-16 text-sm text-muted-foreground">
              Loading the reference…
            </p>
          }
        >
          <ApiReference />
        </Suspense>
      ) : (
        <p className="mx-auto max-w-6xl px-5 py-16 text-sm text-muted-foreground">
          Loading the reference…
        </p>
      )}
    </section>
  );
}
