import { HydrationBoundary } from "@tanstack/react-query";
import { Link, useLoaderData } from "react-router";
import {
  getGetHomeQueryKey,
  getGetHomeQueryOptions,
  getGetIntroQueryKey,
  getGetIntroQueryOptions,
  useGetHome,
} from "~/api/generated/gallery";
import type { PhotoSummary } from "~/api/generated/model";
import { FeaturedCarousel } from "~/components/featured-carousel";
import { HomeIntro } from "~/components/home-intro";
import { fixtureHome, fixtureIntro } from "~/dev/fixtures";
import { fixturesEnabled } from "~/lib/env.server";
import { rethrowAs404 } from "~/lib/http.server";
import { dehydrated, publicQueryClient } from "~/lib/loader.server";

const INTRO_COUNT = 10;

export async function loader() {
  const queryClient = publicQueryClient();

  if (fixturesEnabled()) {
    queryClient.setQueryData(getGetHomeQueryKey(), fixtureHome);
    queryClient.setQueryData(
      getGetIntroQueryKey({ count: INTRO_COUNT }),
      fixtureIntro,
    );

    return {
      dehydratedState: dehydrated(queryClient),
      intro: fixtureIntro,
    };
  }

  try {
    const home = await queryClient.fetchQuery(getGetHomeQueryOptions());
    // The intro is decoration on top of the page, so its failure must not turn
    // the home page into a 404; fall back to whatever is already featured.
    const intro = await queryClient
      .fetchQuery(getGetIntroQueryOptions({ count: INTRO_COUNT }))
      .catch(() => home.featured);

    return {
      dehydratedState: dehydrated(queryClient),
      intro,
    };
  } catch (error) {
    rethrowAs404(error);
  }
}

export default function Home() {
  const { dehydratedState, intro } = useLoaderData<typeof loader>();

  return (
    <HydrationBoundary state={dehydratedState}>
      <HomeContent intro={intro} />
    </HydrationBoundary>
  );
}

function HomeContent({ intro }: { intro: readonly PhotoSummary[] }) {
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
      <HomeIntro photos={intro} />

      <section className="border-b border-rule">
        <div className="mx-auto max-w-6xl px-5 pt-28 pb-20 sm:py-28">
          <h1 className="max-w-3xl font-heading text-5xl leading-[1.05] tracking-tight sm:text-6xl">
            A personal archive, kept open to the curious.
          </h1>
          <p className="mt-6 max-w-xl text-base text-muted-foreground">
            I built this space to give my favourite photographs a place beyond a
            hard drive. It's a personal collection, a small creative project,
            and an ongoing excuse to keep exploring photography. Nothing too
            formal — just things I've seen and wanted to share.
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
          <FeaturedCarousel photos={data.featured} />
        ) : (
          <p className="mt-6 text-muted-foreground">Nothing is featured yet.</p>
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
