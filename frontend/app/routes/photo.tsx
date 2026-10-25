import { HydrationBoundary } from "@tanstack/react-query";
import { Link, useLoaderData } from "react-router";
import type { PhotoDetail } from "~/api/generated/model";
import {
  getGetPhotoQueryKey,
  getGetPhotoQueryOptions,
  useGetPhoto,
} from "~/api/generated/gallery";
import { PhotoMedia } from "~/components/photo-media";
import { RouteError } from "~/components/route-error";
import { fixturePhotoDetail } from "~/dev/fixtures";
import { fixturesEnabled, getImageBaseUrl, getSiteUrl } from "~/lib/env.server";
import { exifEntries } from "~/lib/exif";
import { rethrowAs404 } from "~/lib/http.server";
import {
  derivativeUrl,
  PHOTO_SIZES,
  preferredWidth,
  scaledHeight,
} from "~/lib/images";
import { dehydrated, publicQueryClient } from "~/lib/loader.server";
import { useRootData } from "~/lib/root-data";

import type { Route } from "./+types/photo";

export function ErrorBoundary() {
  return <RouteError />;
}

function copyrightNotice(detail: PhotoDetail): string {
  const explicit = detail.exif.Copyright?.trim();
  if (explicit) {
    return explicit;
  }

  const year = detail.photo.takenAt?.slice(0, 4);
  return year ? `© ${year} Gallery` : "© Gallery";
}

export const meta: Route.MetaFunction = ({ loaderData }) => {
  if (!loaderData) {
    return [{ title: "Photograph — Gallery" }];
  }

  const { photo, caption, ogImage, ogImageWidth, ogImageHeight, pageUrl, jsonLd } =
    loaderData;
  const title = photo.title?.trim() || "Untitled photograph";
  const description =
    photo.altText?.trim() ||
    caption?.trim() ||
    "A photograph from the archive.";

  return [
    { title: `${title} — Gallery` },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: pageUrl },
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
          ...(ogImageHeight
            ? [
                {
                  property: "og:image:height",
                  content: String(ogImageHeight),
                },
              ]
            : []),
          { property: "og:image:alt", content: description },
        ]
      : []),
    { name: "twitter:card", content: "summary_large_image" },
    { name: "robots", content: "index, follow" },
    { "script:ld+json": jsonLd },
  ];
};

export async function loader({ params }: Route.LoaderArgs) {
  const id = params.id;
  const queryClient = publicQueryClient();
  const imageBaseUrl = getImageBaseUrl();

  const finish = (detail: PhotoDetail) => {
    const width = preferredWidth(detail.photo.availableWidths);
    const pageUrl = `${getSiteUrl()}/photos/${detail.photo.id}`;
    const ogImage = width
      ? derivativeUrl(imageBaseUrl, detail.photo.sha256, width, "jpg")
      : null;
    const title = detail.photo.title?.trim() || "Untitled photograph";
    const description =
      detail.photo.altText?.trim() ||
      detail.caption?.trim() ||
      "A photograph from the archive.";

    const jsonLd = {
      "@context": "https://schema.org",
      "@type": "ImageObject",
      name: title,
      description,
      url: pageUrl,
      ...(ogImage && width
        ? {
            contentUrl: ogImage,
            width,
            height: scaledHeight(
              detail.photo.width,
              detail.photo.height,
              width,
            ),
          }
        : {}),
      creator: {
        "@type": detail.exif.Artist ? "Person" : "Organization",
        name: detail.exif.Artist ?? "Gallery",
      },
      creditText: detail.exif.Credit ?? detail.collectionName,
      copyrightNotice: copyrightNotice(detail),
    };

    return {
      dehydratedState: dehydrated(queryClient),
      photo: detail.photo,
      caption: detail.caption,
      ogImage,
      ogImageWidth: width ?? null,
      ogImageHeight: width
        ? scaledHeight(detail.photo.width, detail.photo.height, width)
        : null,
      pageUrl,
      jsonLd,
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
  const { imageBaseUrl } = useRootData();
  const { data } = useGetPhoto(id);

  if (!data) {
    return (
      <div className="mx-auto max-w-4xl px-5 py-24">
        <p className="text-muted-foreground">Loading photograph…</p>
      </div>
    );
  }

  const { photo, caption, collectionSlug, collectionName, exif } = data;
  const title = photo.title?.trim() || "Untitled photograph";
  const entries = exifEntries(exif);

  return (
    <article className="mx-auto max-w-5xl px-5 py-12">
      <nav aria-label="Breadcrumb" className="text-sm text-muted-foreground">
        <Link to="/collections" className="hover:text-fg">
          Collections
        </Link>
        <span aria-hidden="true"> / </span>
        <Link to={`/collections/${collectionSlug}`} className="hover:text-fg">
          {collectionName}
        </Link>
      </nav>

      <figure className="mt-6">
        <div
          className="w-full"
          style={{ backgroundColor: photo.dominantColor }}
        >
          <PhotoMedia
            photo={photo}
            imageBaseUrl={imageBaseUrl}
            sizes={PHOTO_SIZES}
            loading="eager"
            fetchPriority="high"
            imgClassName="h-auto w-full"
          />
        </div>
        <figcaption className="mt-4 flex flex-wrap items-baseline justify-between gap-2 border-b border-rule pb-4 text-sm text-muted-foreground">
          <span>
            {Number(photo.width)} × {Number(photo.height)}
          </span>
          {photo.takenAt ? (
            <time dateTime={photo.takenAt}>{photo.takenAt.slice(0, 10)}</time>
          ) : null}
        </figcaption>
      </figure>

      <header className="mt-8 max-w-3xl">
        <h1 className="font-heading text-4xl tracking-tight">{title}</h1>
        {caption ? <p className="mt-4 text-muted-foreground">{caption}</p> : null}
      </header>

      <section className="mt-10 max-w-3xl">
        <h2 className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
          EXIF
        </h2>
        {entries.length > 0 ? (
          <>
            <dl className="mt-3 divide-y divide-rule border-y border-rule text-sm">
              {entries.map(([key, label, value]) => (
                <div key={key} className="flex justify-between gap-6 py-2">
                  <dt className="text-muted-foreground">{label}</dt>
                  <dd className="break-words text-right">{value}</dd>
                </div>
              ))}
            </dl>
            <details className="mt-3 text-xs text-muted-foreground">
              <summary className="cursor-pointer">Raw EXIF JSON</summary>
              <pre className="mt-2 overflow-x-auto border border-rule p-3 text-xs">
                {JSON.stringify(exif, null, 2)}
              </pre>
            </details>
          </>
        ) : (
          <p className="mt-3 text-sm text-muted-foreground">
            No EXIF metadata was recorded for this photograph.
          </p>
        )}
      </section>

      <nav
        aria-label="Photograph navigation"
        className="mt-12 flex items-center justify-between border-t border-rule pt-4 text-sm"
      >
        {data.previousPhotoId ? (
          <Link
            to={`/photos/${data.previousPhotoId}`}
            rel="prev"
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
            rel="next"
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
