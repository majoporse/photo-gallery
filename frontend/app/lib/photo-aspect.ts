import type { PhotoSummary } from "~/api/generated/model";

/**
 * Width divided by height, with a landscape fallback for incomplete summaries.
 * Both intro layouts size frames from this ratio, so they must agree.
 */
export function photoAspect(
  photo: Pick<PhotoSummary, "width" | "height">,
): number {
  const width = Number(photo.width);
  const height = Number(photo.height);
  if (
    !Number.isFinite(width) ||
    !Number.isFinite(height) ||
    width <= 0 ||
    height <= 0
  ) {
    return 1.5;
  }
  return width / height;
}
