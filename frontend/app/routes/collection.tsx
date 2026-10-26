import { useRef, useState } from "react";
import { HydrationBoundary } from "@tanstack/react-query";
import { Link, useLoaderData } from "react-router";
import {
  getGetCollectionQueryKey,
  getGetCollectionQueryOptions,
  getListCollectionPhotosQueryKey,
  getListCollectionPhotosQueryOptions,
  useGetCollection,
  useListCollectionPhotos,
} from "~/api/generated/gallery";
import { PhotoCard } from "~/components/photo-card";
import { PhotoLightbox } from "~/components/photo-lightbox";
import { Masonry, MasonryItem } from "~/components/masonry";
import { RouteError } from "~/components/route-error";
import { fixtureCollection, fixtureCollectionPhotos } from "~/dev/fixtures";
import { fixturesEnabled } from "~/lib/env.server";
import { rethrowAs404 } from "~/lib/http.server";
import {
  dehydrated,
  parsePage,
  publicQueryClient,
} from "~/lib/loader.server";
import { useRootData } from "~/lib/root-data";

import type { Route } from "./+types/collection";

export function ErrorBoundary() {
  return <RouteError />;
}

export const meta: Route.MetaFunction = ({ loaderData }) => {
  if (!loaderData) {
    return [{ title: "Collection — Gallery" }];
  }

  const { collection } = loaderData;
  return [
    { title: `${collection.name} — Gallery` },
    {
      name: "description",
      content:
        collection.description ?? `${collection.photoCount} photographs.`,
    },
    { property: "og:title", content: collection.name },
    {
      property: "og:description",
      content:
        collection.description ?? `${collection.photoCount} photographs.`,
    },
    { property: "og:type", content: "website" },
    { name: "robots", content: "index, follow" },
  ];
};

export async function loader({ params, request }: Route.LoaderArgs) {
  const slug = params.slug;
  const page = parsePage(new URL(request.url).searchParams.get("page"));
  const queryClient = publicQueryClient();

  if (fixturesEnabled()) {
    const collection = fixtureCollection(slug);
    const photos = fixtureCollectionPhotos(slug, page);
    if (!collection || !photos) {
      throw new Response(null, { status: 404 });
    }

    queryClient.setQueryData(getGetCollectionQueryKey(slug), collection);
    queryClient.setQueryData(
      getListCollectionPhotosQueryKey(slug, { page }),
      photos,
    );

    return {
      dehydratedState: dehydrated(queryClient),
      slug,
      page,
      collection,
    };
  }

  try {
    const collection = await queryClient.fetchQuery(
      getGetCollectionQueryOptions(slug),
    );
    await queryClient.fetchQuery(
      getListCollectionPhotosQueryOptions(slug, { page }),
    );

    return {
      dehydratedState: dehydrated(queryClient),
      slug,
      page,
      collection,
    };
  } catch (error) {
    rethrowAs404(error);
  }
}

export default function Collection() {
  const { dehydratedState, slug, page } = useLoaderData<typeof loader>();

  return (
    <HydrationBoundary state={dehydratedState}>
      <CollectionContent slug={slug} page={page} />
    </HydrationBoundary>
  );
}

function CollectionContent({ slug, page }: { slug: string; page: number }) {
  const { imageBaseUrl } = useRootData();
  const { data: collection } = useGetCollection(slug);
  const { data: photos } = useListCollectionPhotos(slug, { page });
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const triggerRef = useRef<HTMLAnchorElement | null>(null);

  if (!collection || !photos) {
    return (
      <div className="mx-auto max-w-6xl px-5 py-24">
        <p className="text-muted-foreground">Loading collection…</p>
      </div>
    );
  }

  const totalPages = Math.max(
    1,
    Math.ceil(Number(photos.total) / Number(photos.pageSize)),
  );

  const closeLightbox = () => {
    setOpenIndex(null);
    requestAnimationFrame(() => triggerRef.current?.focus());
  };

  return (
    <section className="mx-auto max-w-6xl px-5 py-16">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        Collection · {photos.total}{" "}
        {Number(photos.total) === 1 ? "photograph" : "photographs"}
      </p>
      <h1 className="mt-3 font-heading text-5xl tracking-tight">
        {collection.name}
      </h1>
      {collection.description ? (
        <p className="mt-4 max-w-2xl text-muted-foreground">
          {collection.description}
        </p>
      ) : null}

      {photos.items.length > 0 ? (
        <Masonry className="mt-10">
          {/* The index comes from the photo array, not the DOM, so the lightbox
              still opens the right slide when CSS columns reorder the tiles. */}
          {photos.items.map((photo, index) => (
            <MasonryItem key={photo.id}>
              <PhotoCard
                photo={photo}
                index={index}
                priority={index === 0}
                naturalAspect
                onOpen={(openedIndex, trigger) => {
                  triggerRef.current = trigger;
                  setOpenIndex(openedIndex);
                }}
              />
            </MasonryItem>
          ))}
        </Masonry>
      ) : (
        <p className="mt-10 text-muted-foreground">
          {Number(photos.total) > 0
            ? "No photographs on this page."
            : "This collection has no photographs yet."}
        </p>
      )}

      {totalPages > 1 ? (
        <nav
          aria-label="Collection pages"
          className="mt-12 flex items-center justify-between border-t border-rule pt-4 text-sm"
        >
          {page > 1 ? (
            <Link
              to={`?page=${page - 1}`}
              rel="prev"
              className="border-b border-accent pb-0.5"
            >
              Previous
            </Link>
          ) : (
            <span />
          )}
          <span className="text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          {page < totalPages ? (
            <Link
              to={`?page=${page + 1}`}
              rel="next"
              className="border-b border-accent pb-0.5"
            >
              Next
            </Link>
          ) : (
            <span />
          )}
        </nav>
      ) : null}

      <PhotoLightbox
        photos={photos.items}
        imageBaseUrl={imageBaseUrl}
        index={openIndex}
        collectionName={collection.name}
        onClose={closeLightbox}
      />
    </section>
  );
}
