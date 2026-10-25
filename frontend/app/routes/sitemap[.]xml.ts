import {
  getListCollectionPhotosQueryOptions,
  getListCollectionsQueryOptions,
} from "~/api/generated/gallery";
import { fixtureCollectionPhotos, fixtureCollections } from "~/dev/fixtures";
import { fixturesEnabled, getSiteUrl } from "~/lib/env.server";
import { publicQueryClient } from "~/lib/loader.server";
import { absoluteUrl, sitemapXml, type SitemapEntry } from "~/lib/sitemap";

const PAGE_SIZE = 100;

function staticEntries(siteUrl: string): SitemapEntry[] {
  return [
    { loc: absoluteUrl(siteUrl, "/"), changefreq: "daily", priority: "1.0" },
    {
      loc: absoluteUrl(siteUrl, "/collections"),
      changefreq: "weekly",
      priority: "0.8",
    },
    {
      loc: absoluteUrl(siteUrl, "/about"),
      changefreq: "monthly",
      priority: "0.5",
    },
  ];
}

async function catalogueEntries(siteUrl: string): Promise<SitemapEntry[]> {
  const entries: SitemapEntry[] = [];

  if (fixturesEnabled()) {
    for (const collection of fixtureCollections.items) {
      const collectionUrl = absoluteUrl(
        siteUrl,
        `/collections/${collection.slug}`,
      );
      entries.push({
        loc: collectionUrl,
        changefreq: "weekly",
        priority: "0.7",
      });

      let page = 1;
      let totalPages = 1;
      do {
        const photos = fixtureCollectionPhotos(collection.slug, page);
        if (!photos) {
          break;
        }

        for (const photo of photos.items) {
          entries.push({
            loc: absoluteUrl(siteUrl, `/photos/${photo.id}`),
            changefreq: "monthly",
            priority: "0.6",
          });
        }

        totalPages = Math.max(
          1,
          Math.ceil(Number(photos.total) / Number(photos.pageSize)),
        );
        page += 1;
      } while (page <= totalPages);
    }

    return entries;
  }

  const queryClient = publicQueryClient();
  const collections = await queryClient.fetchQuery(
    getListCollectionsQueryOptions({ page: 1, pageSize: PAGE_SIZE }),
  );

  for (const collection of collections.items) {
    const collectionPath = `/collections/${collection.slug}`;
    entries.push({
      loc: absoluteUrl(siteUrl, collectionPath),
      changefreq: "weekly",
      priority: "0.7",
    });

    let page = 1;
    let totalPages = 1;
    do {
      const photos = await queryClient.fetchQuery(
        getListCollectionPhotosQueryOptions(collection.slug, {
          page,
          pageSize: PAGE_SIZE,
        }),
      );

      for (const photo of photos.items) {
        entries.push({
          loc: absoluteUrl(siteUrl, `/photos/${photo.id}`),
          changefreq: "monthly",
          priority: "0.6",
        });
      }

      if (page > 1) {
        entries.push({
          loc: `${absoluteUrl(siteUrl, collectionPath)}?page=${page}`,
          changefreq: "weekly",
          priority: "0.4",
        });
      }

      totalPages = Math.max(
        1,
        Math.ceil(Number(photos.total) / Number(photos.pageSize)),
      );
      page += 1;
    } while (page <= totalPages);
  }

  return entries;
}

export async function loader() {
  const siteUrl = getSiteUrl();
  let entries = staticEntries(siteUrl);

  try {
    entries = [...entries, ...(await catalogueEntries(siteUrl))];
  } catch (error) {
    console.error("sitemap: could not read the catalogue", error);
  }

  return new Response(sitemapXml(entries), {
    headers: {
      "Content-Type": "application/xml; charset=utf-8",
      "Cache-Control": "public, max-age=3600",
    },
  });
}
