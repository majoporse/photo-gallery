export const DERIVATIVE_WIDTHS = [320, 640, 1280, 2048] as const;
export const DERIVATIVE_FORMATS = ["avif", "webp", "jpg"] as const;

export type DerivativeFormat = (typeof DERIVATIVE_FORMATS)[number];
export type DerivativeWidth = (typeof DERIVATIVE_WIDTHS)[number];

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

export function dimensionLabel(
  width: number | string,
  height: number | string,
): string {
  return `${width} × ${height}`;
}
