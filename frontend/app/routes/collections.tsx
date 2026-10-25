import { HydrationBoundary } from "@tanstack/react-query";
import { useLoaderData } from "react-router";
import {
  getListCollectionsQueryKey,
  getListCollectionsQueryOptions,
  useListCollections,
} from "~/api/generated/gallery";
import { CollectionCard } from "~/components/collection-card";
import { fixtureCollections } from "~/dev/fixtures";
import { fixturesEnabled } from "~/lib/env.server";
import { rethrowAs404 } from "~/lib/http.server";
import { dehydrated, publicQueryClient } from "~/lib/loader.server";

import type { Route } from "./+types/collections";

export const meta: Route.MetaFunction = () => {
  return [
    { title: "Collections — Gallery" },
    {
      name: "description",
      content: "Every collection in the archive, in publication order.",
    },
    { property: "og:title", content: "Collections — Gallery" },
    { property: "og:type", content: "website" },
    { name: "robots", content: "index, follow" },
  ];
};

export async function loader() {
  const queryClient = publicQueryClient();

  if (fixturesEnabled()) {
    queryClient.setQueryData(getListCollectionsQueryKey(), fixtureCollections);
  } else {
    try {
      await queryClient.fetchQuery(getListCollectionsQueryOptions());
    } catch (error) {
      rethrowAs404(error);
    }
  }

  return { dehydratedState: dehydrated(queryClient) };
}

export default function Collections() {
  const { dehydratedState } = useLoaderData<typeof loader>();

  return (
    <HydrationBoundary state={dehydratedState}>
      <CollectionsContent />
    </HydrationBoundary>
  );
}

function CollectionsContent() {
  const { data } = useListCollections();

  if (!data) {
    return (
      <div className="mx-auto max-w-6xl px-5 py-24">
        <p className="text-muted-foreground">Loading collections…</p>
      </div>
    );
  }

  return (
    <section className="mx-auto max-w-6xl px-5 py-16">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        {data.total} {data.total === 1 ? "collection" : "collections"}
      </p>
      <h1 className="mt-3 font-heading text-5xl tracking-tight">Collections</h1>

      {data.items.length > 0 ? (
        <ul className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map((collection) => (
            <li key={collection.id}>
              <CollectionCard collection={collection} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-10 text-muted-foreground">
          No collections have been published yet.
        </p>
      )}
    </section>
  );
}
