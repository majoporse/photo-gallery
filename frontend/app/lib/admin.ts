import type {
  CollectionAdminView,
  EditPhoto,
  PhotoDetail,
} from "~/api/generated/model";

export const UPLOAD_REJECTION_REASONS: Record<string, string> = {
  FileTooLarge: "Larger than the 50 MB limit.",
  UnsupportedFormat: "Not an image format the gallery recognises.",
  TooSmall: "Shorter than 640 px on the long edge.",
  DimensionsTooLarge: "More than 120 megapixels.",
  CorruptImage: "The file could not be read as an image.",
  ProcessingFailed: "Processing kept failing. The original is kept.",
  MissingBytes: "The uploaded file was gone before it could be read.",
};

// Older payloads sent the enum as its ordinal; the API now sends the name.
const UPLOAD_REJECTION_LEGACY_ORDER = [
  "FileTooLarge",
  "UnsupportedFormat",
  "TooSmall",
  "DimensionsTooLarge",
  "CorruptImage",
  "ProcessingFailed",
];

export function uploadRejectionReason(value: string | number): string {
  const name =
    typeof value === "number"
      ? UPLOAD_REJECTION_LEGACY_ORDER[value]
      : UPLOAD_REJECTION_REASONS[value]
        ? value
        : undefined;

  return (
    (name ? UPLOAD_REJECTION_REASONS[name] : undefined) ??
    `The API rejected this file (reason code ${String(value)}).`
  );
}

const UPLOAD_STATUS_LABELS: Record<string, string> = {
  Pending: "Waiting",
  Uploaded: "Uploaded",
  Queued: "Queued",
  Succeeded: "Added",
  Rejected: "Rejected",
  Expired: "Expired",
};

export function uploadStatusLabel(status: string): string {
  return UPLOAD_STATUS_LABELS[status] ?? status;
}

export function isTerminalUploadStatus(status: string): boolean {
  return status === "Succeeded" || status === "Rejected" || status === "Expired";
}

/**
 * The browser knows the upload percentage, the worker knows only that it is
 * busy, so one bar carries both phases: bytes first, then an indeterminate
 * processing sweep until the server settles on a terminal status.
 */
export type UploadPhase =
  | "waiting"
  | "uploading"
  | "processing"
  | "succeeded"
  | "failed";

const PROCESSING_STATUSES = new Set(["Uploaded", "Queued", "Processing"]);

export function uploadPhase(status: string): UploadPhase {
  if (status === "uploading") {
    return "uploading";
  }

  if (status === "queued" || status === "Pending") {
    return "waiting";
  }

  if (PROCESSING_STATUSES.has(status)) {
    return "processing";
  }

  return status === "Succeeded" ? "succeeded" : "failed";
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
