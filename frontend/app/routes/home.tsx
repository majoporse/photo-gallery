import { HydrationBoundary } from "@tanstack/react-query";
import { Link, useLoaderData } from "react-router";
import {
  getGetHomeQueryKey,
  getGetHomeQueryOptions,
  useGetHome,
} from "~/api/generated/gallery";
import { PhotoCard } from "~/components/photo-card";
import { fixtureHome } from "~/dev/fixtures";
import { fixturesEnabled } from "~/lib/env.server";
import { rethrowAs404 } from "~/lib/http.server";
import { dehydrated, publicQueryClient } from "~/lib/loader.server";

export async function loader() {
  const queryClient = publicQueryClient();

  if (fixturesEnabled()) {
    queryClient.setQueryData(getGetHomeQueryKey(), fixtureHome);
  } else {
    try {
      await queryClient.fetchQuery(getGetHomeQueryOptions());
    } catch (error) {
      rethrowAs404(error);
    }
  }

  return { dehydratedState: dehydrated(queryClient) };
}

export default function Home() {
  const { dehydratedState } = useLoaderData<typeof loader>();

  return (
    <HydrationBoundary state={dehydratedState}>
      <HomeContent />
    </HydrationBoundary>
  );
}

function HomeContent() {
  const { data } = useGetHome();

  if (!data) {
    return (
      <div className="mx-auto max-w-6xl px-5 py-24">
        <p className="text-muted-foreground">Loading the gallery…</p>
      </div>
    );
  }

  return (
    <>
      <section className="border-b border-rule">
        <div className="mx-auto max-w-6xl px-5 py-20 sm:py-28">
          <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
            A personal archive
          </p>
          <h1 className="mt-5 max-w-3xl font-heading text-5xl leading-[1.05] tracking-tight sm:text-6xl">
            Photographs, kept in collections and shown at full size.
          </h1>
          <p className="mt-6 max-w-xl text-base text-muted-foreground">
            No feeds, no accounts, no algorithm. Just a growing record of places
            and things worth looking at twice.
          </p>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-5 py-16">
        <div className="flex items-baseline justify-between gap-4 border-b border-rule pb-3">
          <h2 className="font-heading text-2xl tracking-tight">Featured</h2>
          <Link
            to="/collections"
            className="text-sm text-muted-foreground hover:text-fg"
          >
            All collections
          </Link>
        </div>

        {data.featured.length > 0 ? (
          <ul className="mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {data.featured.map((photo) => (
              <li key={photo.id}>
                <PhotoCard photo={photo} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-6 text-muted-foreground">
            Nothing is featured yet.
          </p>
        )}
      </section>

      <section className="mx-auto max-w-6xl px-5 pb-8">
        <h2 className="border-b border-rule pb-3 font-heading text-2xl tracking-tight">
          Collections
        </h2>
        <ul className="mt-2 divide-y divide-rule border-b border-rule">
          {data.collections.map((collection) => (
            <li key={collection.id}>
              <Link
                to={`/collections/${collection.slug}`}
                className="flex items-baseline justify-between gap-4 py-4 hover:text-accent"
              >
                <span className="font-heading text-xl">{collection.name}</span>
                <span className="text-sm text-muted-foreground">
                  {collection.photoCount} photographs
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </>
  );
}
