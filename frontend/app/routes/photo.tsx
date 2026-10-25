import { HydrationBoundary } from "@tanstack/react-query";
import { Link, useLoaderData } from "react-router";
import type { PhotoDetail } from "~/api/generated/model";
import {
  getGetPhotoQueryKey,
  getGetPhotoQueryOptions,
  useGetPhoto,
} from "~/api/generated/gallery";
import { fixturePhotoDetail } from "~/dev/fixtures";
import { fixturesEnabled, getImageBaseUrl, getSiteUrl } from "~/lib/env.server";
import { rethrowAs404 } from "~/lib/http.server";
import { derivativeUrl, pickWidth } from "~/lib/images";
import { dehydrated, publicQueryClient } from "~/lib/loader.server";

import type { Route } from "./+types/photo";

export const meta: Route.MetaFunction = ({ loaderData }) => {
  if (!loaderData) {
    return [{ title: "Photograph — Gallery" }];
  }

  const { photo, caption, ogImage, ogImageWidth, pageUrl } = loaderData;
  const title = photo.title ?? "Untitled photograph";
  const description =
    photo.altText ?? caption ?? "A photograph from the archive.";

  return [
    { title: `${title} — Gallery` },
    { name: "description", content: description },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:type", content: "article" },
    { property: "og:url", content: pageUrl },
    ...(ogImage
      ? [
          { property: "og:image", content: ogImage },
          ...(ogImageWidth
            ? [
                {
                  property: "og:image:width",
                  content: String(ogImageWidth),
                },
              ]
            : []),
          { property: "og:image:alt", content: description },
        ]
      : []),
    { name: "twitter:card", content: "summary_large_image" },
    { name: "robots", content: "index, follow" },
  ];
};

export async function loader({ params }: Route.LoaderArgs) {
  const id = params.id;
  const queryClient = publicQueryClient();
  const imageBaseUrl = getImageBaseUrl();

  const finish = (detail: PhotoDetail) => {
    const width = pickWidth(detail.photo.availableWidths, 1280);
    return {
      dehydratedState: dehydrated(queryClient),
      photo: detail.photo,
      caption: detail.caption,
      ogImage: width
        ? derivativeUrl(imageBaseUrl, detail.photo.sha256, width, "jpg")
        : null,
      ogImageWidth: width ?? null,
      pageUrl: `${getSiteUrl()}/photos/${detail.photo.id}`,
    };
  };

  if (fixturesEnabled()) {
    const detail = fixturePhotoDetail(id);
    if (!detail) {
      throw new Response(null, { status: 404 });
    }

    queryClient.setQueryData(getGetPhotoQueryKey(id), detail);
    return finish(detail);
  }

  try {
    const detail = await queryClient.fetchQuery(getGetPhotoQueryOptions(id));
    return finish(detail);
  } catch (error) {
    rethrowAs404(error);
  }
}

export default function Photo() {
  const { dehydratedState, photo } = useLoaderData<typeof loader>();

  return (
    <HydrationBoundary state={dehydratedState}>
      <PhotoContent id={photo.id} />
    </HydrationBoundary>
  );
}

function PhotoContent({ id }: { id: string }) {
  const { data } = useGetPhoto(id);

  if (!data) {
    return (
      <div className="mx-auto max-w-4xl px-5 py-24">
        <p className="text-muted-foreground">Loading photograph…</p>
      </div>
    );
  }

  const { photo, caption, collectionSlug, collectionName, exif } = data;
  const title = photo.title ?? "Untitled photograph";
  const exifEntries = Object.entries(exif);

  return (
    <article className="mx-auto max-w-5xl px-5 py-12">
      <nav aria-label="Breadcrumb" className="text-sm text-muted-foreground">
        <Link to="/collections" className="hover:text-fg">
          Collections
        </Link>
        <span aria-hidden="true"> / </span>
        <Link
          to={`/collections/${collectionSlug}`}
          className="hover:text-fg"
        >
          {collectionName}
        </Link>
      </nav>

      <figure className="mt-6">
        <div
          className="aspect-[4/3] w-full"
          style={{ backgroundColor: photo.dominantColor }}
          role="img"
          aria-label={photo.altText ?? title}
        />
        <figcaption className="mt-4 flex flex-wrap items-baseline justify-between gap-2 border-b border-rule pb-4 text-sm text-muted-foreground">
          <span>
            {Number(photo.width)} × {Number(photo.height)}
          </span>
          {photo.takenAt ? (
            <time dateTime={photo.takenAt}>
              {photo.takenAt.slice(0, 10)}
            </time>
          ) : null}
        </figcaption>
      </figure>

      <header className="mt-8 max-w-3xl">
        <h1 className="font-heading text-4xl tracking-tight">{title}</h1>
        {caption ? (
          <p className="mt-4 text-muted-foreground">{caption}</p>
        ) : null}
      </header>

      {exifEntries.length > 0 ? (
        <section className="mt-10 max-w-3xl">
          <h2 className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
            EXIF
          </h2>
          <dl className="mt-3 divide-y divide-rule border-y border-rule text-sm">
            {exifEntries.map(([key, value]) => (
              <div key={key} className="flex justify-between gap-6 py-2">
                <dt className="text-muted-foreground capitalize">
                  {key.replaceAll("_", " ")}
                </dt>
                <dd className="text-right">{value}</dd>
              </div>
            ))}
          </dl>
        </section>
      ) : null}

      <nav
        aria-label="Photograph navigation"
        className="mt-12 flex items-center justify-between border-t border-rule pt-4 text-sm"
      >
        {data.previousPhotoId ? (
          <Link
            to={`/photos/${data.previousPhotoId}`}
            className="border-b border-accent pb-0.5"
          >
            Previous
          </Link>
        ) : (
          <span />
        )}
        {data.nextPhotoId ? (
          <Link
            to={`/photos/${data.nextPhotoId}`}
            className="border-b border-accent pb-0.5"
          >
            Next
          </Link>
        ) : (
          <span />
        )}
      </nav>
    </article>
  );
}
