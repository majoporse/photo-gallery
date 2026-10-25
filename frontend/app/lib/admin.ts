import type {
  CollectionAdminView,
  EditPhoto,
  PhotoDetail,
} from "~/api/generated/model";

export const UPLOAD_REJECTION_REASONS: Record<number, string> = {
  0: "Larger than the 50 MB limit.",
  1: "Not an image format the gallery recognises.",
  2: "Shorter than 640 px on the long edge.",
  3: "More than 120 megapixels.",
  4: "The file could not be read as an image.",
};

const UPLOAD_REJECTION_BY_NAME: Record<string, number> = {
  FileTooLarge: 0,
  UnsupportedFormat: 1,
  TooSmall: 2,
  DimensionsTooLarge: 3,
  CorruptImage: 4,
};

export function uploadRejectionReason(value: number | string): string {
  const code =
    typeof value === "string"
      ? (UPLOAD_REJECTION_BY_NAME[value] ?? Number(value))
      : value;
  return (
    UPLOAD_REJECTION_REASONS[code] ??
    `The API rejected this file (reason code ${String(value)}).`
  );
}

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function isValidSlug(slug: string): boolean {
  return slug.length <= 120 && SLUG_PATTERN.test(slug);
}

/** Mirrors the API's slug rule closely enough to catch mistakes before submit. */
export function slugFromName(name: string): string {
  return name
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 120)
    .replace(/-+$/g, "");
}

export function textOrNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

function pad(value: number): string {
  return String(value).padStart(2, "0");
}

/** ISO timestamp to the local `datetime-local` value a form expects. */
export function formatDateTimeLocal(value: string | null | undefined): string {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return [
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`,
    `${pad(date.getHours())}:${pad(date.getMinutes())}`,
  ].join("T");
}

/** `datetime-local` value to the ISO/UTC instant the API stores. */
export function toIsoDateTime(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") {
    return null;
  }

  const date = new Date(trimmed);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

export interface PhotoFormState {
  title: string;
  caption: string;
  altText: string;
  takenAt: string;
  featured: boolean;
  featuredRank: string;
  collectionId: string;
}

export type PhotoPayloadResult =
  | { ok: true; payload: EditPhoto }
  | { ok: false; error: string };

/**
 * PATCH replaces every editable field, so this always sends the whole form.
 * An empty text field becomes null, which is how the owner clears it.
 */
export function buildPhotoEditPayload(
  form: PhotoFormState,
): PhotoPayloadResult {
  const takenAt = toIsoDateTime(form.takenAt);
  if (form.takenAt.trim() !== "" && takenAt === null) {
    return { ok: false, error: "That taken-at value is not a valid date." };
  }

  let featuredRank: number | null = null;
  if (form.featured) {
    const rank = form.featuredRank.trim();
    if (!/^\d+$/.test(rank) || !Number.isSafeInteger(Number(rank))) {
      return {
        ok: false,
        error: "A featured photo needs a rank of zero or a larger whole number.",
      };
    }
    featuredRank = Number(rank);
  }

  if (form.collectionId === "") {
    return { ok: false, error: "Choose the collection this photo belongs to." };
  }

  return {
    ok: true,
    payload: {
      title: textOrNull(form.title),
      caption: textOrNull(form.caption),
      altText: textOrNull(form.altText),
      takenAt,
      featuredRank,
      collectionId: form.collectionId,
    },
  };
}

/**
 * The API's photo detail does not expose `featuredRank`, so a grid toggle has
 * to rebuild the full PATCH body from the public detail plus the known
 * collection.
 */
export function payloadFromPhotoDetail(
  detail: PhotoDetail,
  collectionId: string,
  featuredRank: number | null,
): EditPhoto {
  return {
    title: detail.photo.title,
    caption: detail.caption,
    altText: detail.photo.altText,
    takenAt: detail.photo.takenAt,
    featuredRank,
    collectionId,
  };
}

export function byPosition(
  a: CollectionAdminView,
  b: CollectionAdminView,
): number {
  return Number(a.position) - Number(b.position);
}

export function liveCollections(
  collections: readonly CollectionAdminView[],
): CollectionAdminView[] {
  return collections.filter((collection) => !collection.deletedAt).sort(byPosition);
}

export function hiddenCollections(
  collections: readonly CollectionAdminView[],
): CollectionAdminView[] {
  return collections.filter((collection) => collection.deletedAt).sort(byPosition);
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}
