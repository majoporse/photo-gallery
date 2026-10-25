export const DERIVATIVE_WIDTHS = [320, 640, 1280, 2048] as const;
export const DERIVATIVE_FORMATS = ["avif", "webp", "jpg"] as const;

export type DerivativeFormat = (typeof DERIVATIVE_FORMATS)[number];
export type DerivativeWidth = (typeof DERIVATIVE_WIDTHS)[number];

export const FORMAT_MIME_TYPE: Record<DerivativeFormat, string> = {
  avif: "image/avif",
  webp: "image/webp",
  jpg: "image/jpeg",
};

/**
 * Grid tracks: one column below 640px, two up to 1024px, then three inside the
 * 72rem shell (about 356px each after padding and gaps).
 */
export const GRID_SIZES =
  "(min-width: 1024px) 356px, (min-width: 640px) calc(50vw - 30px), calc(100vw - 40px)";
export const CAROUSEL_SIZES =
  "(min-width: 1024px) 645px, (min-width: 640px) calc(72vw - 20px), calc(100vw - 20px)";
export const PHOTO_SIZES =
  "(min-width: 1024px) 984px, calc(100vw - 40px)";

export function derivativeUrl(
  imageBaseUrl: string,
  sha256: string,
  width: number,
  format: DerivativeFormat = "jpg",
): string {
  const base = imageBaseUrl.replace(/\/$/, "");
  return `${base}/photos/derivatives/${encodeURIComponent(sha256)}/${width}w.${format}`;
}

export function toWidth(value: number | string): number | undefined {
  const width = typeof value === "number" ? value : Number(value);
  return Number.isInteger(width) && width > 0 ? width : undefined;
}

export function normalizeWidths(
  widths: readonly (number | string)[],
): number[] {
  return [...new Set(widths.flatMap((value) => toWidth(value) ?? []))].sort(
    (a, b) => a - b,
  );
}

/**
 * The ladder is an arbitrary subset of the canonical widths, so never assume
 * 1280 exists. Pick the largest width that does not upscale, else the smallest.
 */
export function pickWidth(
  widths: readonly (number | string)[],
  requested: number,
): number | undefined {
  const available = normalizeWidths(widths);
  const fitting = available.filter((width) => width <= requested);
  return fitting.at(-1) ?? available[0];
}

export function buildSrcSet(
  imageBaseUrl: string,
  sha256: string,
  widths: readonly (number | string)[],
  format: DerivativeFormat,
): string {
  return normalizeWidths(widths)
    .map(
      (width) =>
        `${derivativeUrl(imageBaseUrl, sha256, width, format)} ${width}w`,
    )
    .join(", ");
}

export function largestWidth(
  widths: readonly (number | string)[],
): number | undefined {
  return normalizeWidths(widths).at(-1);
}

/**
 * The width used for non-srcset consumers (OG, lightbox fallback, JSON-LD):
 * prefer 1280, otherwise the largest derivative that exists.
 */
export function preferredWidth(
  widths: readonly (number | string)[],
): number | undefined {
  const available = normalizeWidths(widths);
  return pickWidth(available, 1280) ?? available.at(-1);
}

/** Height of a derivative, preserving the original aspect ratio. */
export function scaledHeight(
  width: number | string,
  height: number | string,
  targetWidth: number,
): number {
  const sourceWidth = Number(width);
  const sourceHeight = Number(height);

  if (!Number.isFinite(sourceWidth) || sourceWidth <= 0 || !Number.isFinite(sourceHeight)) {
    return 0;
  }

  return Math.max(1, Math.round((sourceHeight * targetWidth) / sourceWidth));
}

/**
 * Every photograph gets a description. There is no decorative image on this
 * site, so `alt=""` would hide content from screen readers.
 */
export function photoAltText(photo: {
  altText?: string | null;
  title?: string | null;
}): string {
  const alt = photo.altText?.trim();
  if (alt) {
    return alt;
  }

  const title = photo.title?.trim();
  return title || "Untitled photograph";
}

export function dimensionLabel(
  width: number | string,
  height: number | string,
): string {
  return `${width} × ${height}`;
}
