import type {
  CollectionSummary,
  HomeFeed,
  PagedResultOfCollectionSummary,
  PagedResultOfPhotoSummary,
  PhotoDetail,
  PhotoRef,
  PhotoSummary,
} from "~/api/generated/model";

export const FIXTURE_PHOTO_ID = "01JQZ5F8S4M2X9K7C3V1B6N0A1";

interface PhotoSeed {
  id: string;
  collectionSlug: string;
  title: string;
  altText: string;
  caption: string | null;
  width: number;
  height: number;
  availableWidths: number[];
  dominantColor: string;
  takenAt: string;
  seed: number;
}

function sha256(seed: number): string {
  return seed.toString(16).padStart(2, "0").repeat(32);
}

function photo(seed: PhotoSeed): PhotoSummary {
  return {
    id: seed.id,
    sha256: sha256(seed.seed),
    width: seed.width,
    height: seed.height,
    availableWidths: seed.availableWidths,
    dominantColor: seed.dominantColor,
    title: seed.title,
    altText: seed.altText,
    featuredRank: null,
    takenAt: seed.takenAt,
  };
}

const photoSeeds: PhotoSeed[] = [
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A1",
    collectionSlug: "quiet-coast",
    title: "Tide line, first light",
    altText: "Dark rocks meeting a pale sea under a low morning sky",
    caption: "The tide had just turned; nothing else moved for an hour.",
    width: 6000,
    height: 4000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#5b6d72",
    takenAt: "2025-04-12T06:14:00Z",
    seed: 1,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A2",
    collectionSlug: "quiet-coast",
    title: "Fog bank",
    altText: "A bank of fog drifting across a grey shoreline",
    caption: "Visibility fell to about forty metres before the fog lifted.",
    width: 6000,
    height: 4000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#9aa4a6",
    takenAt: "2025-04-12T07:02:00Z",
    seed: 2,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A3",
    collectionSlug: "quiet-coast",
    title: "Low water",
    altText: "Wet sand reflecting an empty sky",
    caption: null,
    width: 4000,
    height: 6000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#8c8778",
    takenAt: "2025-04-13T18:40:00Z",
    seed: 3,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A4",
    collectionSlug: "quiet-coast",
    title: "Salt haze",
    altText: "A blurred horizon with salt spray on the lens",
    caption: "Shot into the wind, which is why the horizon is soft.",
    width: 4000,
    height: 4000,
    availableWidths: [320, 640, 1280],
    dominantColor: "#b0aa9c",
    takenAt: "2025-04-14T05:58:00Z",
    seed: 4,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A5",
    collectionSlug: "northern-light",
    title: "Pale green over the ridge",
    altText: "Green aurora above a dark mountain ridge",
    caption: "Forty seconds at f/2, and still the stars are sharp.",
    width: 6000,
    height: 4000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#314b3f",
    takenAt: "2025-09-02T23:21:00Z",
    seed: 5,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A6",
    collectionSlug: "northern-light",
    title: "Cabin window",
    altText: "A lit cabin window against deep blue snow",
    caption: "The only warm light for twenty kilometres.",
    width: 4000,
    height: 6000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#24364d",
    takenAt: "2025-09-03T22:10:00Z",
    seed: 6,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A7",
    collectionSlug: "northern-light",
    title: "Star field",
    altText: "A dense field of stars above bare birch trees",
    caption: null,
    width: 6000,
    height: 4000,
    availableWidths: [320, 640, 1280],
    dominantColor: "#1b2230",
    takenAt: "2025-09-04T00:04:00Z",
    seed: 7,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A8",
    collectionSlug: "still-life",
    title: "Three pears",
    altText: "Three pears on a linen cloth beside a window",
    caption: "North light, no fill, one sheet of linen.",
    width: 5000,
    height: 4000,
    availableWidths: [320, 640, 1280, 2048],
    dominantColor: "#7d7a4c",
    takenAt: "2025-06-18T10:31:00Z",
    seed: 8,
  },
  {
    id: "01JQZ5F8S4M2X9K7C3V1B6N0A9",
    collectionSlug: "still-life",
    title: "Chipped cup",
    altText: "A chipped white cup on a dark wooden table",
    caption: "The chip is the reason the photograph exists.",
    width: 4000,
    height: 4000,
    availableWidths: [320, 640, 1280],
    dominantColor: "#8d887e",
    takenAt: "2025-06-18T11:05:00Z",
    seed: 9,
  },
];

const photos = photoSeeds.map(photo);
const photoById = new Map(photos.map((item) => [item.id, item]));

const collectionSeeds = [
  {
    slug: "quiet-coast",
    name: "Quiet Coast",
    description: "Four mornings on the same stretch of shoreline.",
  },
  {
    slug: "northern-light",
    name: "Northern Light",
    description: "Three nights waiting for weather that never quite broke.",
  },
  {
    slug: "still-life",
    name: "Still Life",
    description: "Objects on a table, photographed until they made sense.",
  },
] as const;

function coverFor(slug: string): PhotoRef | null {
  const first = photos.find((item) => photoSeedsFor(item.id)?.collectionSlug === slug);
  if (!first) {
    return null;
  }

  return {
    id: first.id,
    sha256: first.sha256,
    width: first.width,
    height: first.height,
  };
}

function photoSeedsFor(id: string): PhotoSeed | undefined {
  return photoSeeds.find((seed) => seed.id === id);
}

const collections: CollectionSummary[] = collectionSeeds.map((seed) => ({
  id: `collection-${seed.slug}`,
  slug: seed.slug,
  name: seed.name,
  description: seed.description,
  photoCount: photos.filter(
    (item) => photoSeedsFor(item.id)?.collectionSlug === seed.slug,
  ).length,
  cover: coverFor(seed.slug),
}));

const collectionBySlug = new Map(
  collections.map((collection) => [collection.slug, collection]),
);

const details = new Map<string, PhotoDetail>();
for (const collection of collections) {
  const members = photos.filter(
    (item) => photoSeedsFor(item.id)?.collectionSlug === collection.slug,
  );

  members.forEach((item, index) => {
    const seed = photoSeedsFor(item.id);
    if (!seed) {
      return;
    }

    details.set(item.id, {
      photo: item,
      caption: seed.caption || null,
      collectionSlug: collection.slug,
      collectionName: collection.name,
      exif: {
        camera: "Fixture Camera",
        lens: "Fixture 35mm",
        aperture: "f/2",
        shutter: "1/125",
        iso: "200",
      },
      previousPhotoId: members[index - 1]?.id ?? null,
      nextPhotoId: members[index + 1]?.id ?? null,
    });
  });
}

export const fixtureHome: HomeFeed = {
  featured: [photos[0], photos[4], photos[7]].filter(
    (item): item is PhotoSummary => item !== undefined,
  ),
  collections,
};

export const fixtureCollections: PagedResultOfCollectionSummary = {
  items: collections,
  page: 1,
  pageSize: 24,
  total: collections.length,
};

export function fixtureCollection(
  slug: string,
): CollectionSummary | undefined {
  return collectionBySlug.get(slug);
}

export function fixtureCollectionPhotos(
  slug: string,
  page: number,
): PagedResultOfPhotoSummary | undefined {
  if (!collectionBySlug.has(slug)) {
    return undefined;
  }

  const items = photos.filter(
    (item) => photoSeedsFor(item.id)?.collectionSlug === slug,
  );
  const pageSize = 24;
  const start = (page - 1) * pageSize;

  return {
    items: items.slice(start, start + pageSize),
    page,
    pageSize,
    total: items.length,
  };
}

export function fixturePhotoDetail(id: string): PhotoDetail | undefined {
  return details.get(id);
}

export function fixturePhotoSummary(id: string): PhotoSummary | undefined {
  return photoById.get(id);
}
