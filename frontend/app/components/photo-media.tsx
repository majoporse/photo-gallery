import type { PhotoSummary } from "~/api/generated/model";
import {
  buildSrcSet,
  derivativeUrl,
  DERIVATIVE_FORMATS,
  DERIVATIVE_WIDTHS,
  FORMAT_MIME_TYPE,
  normalizeWidths,
  photoAltText,
  pickWidth,
} from "~/lib/images";

type PhotoSource = Pick<PhotoSummary, "sha256" | "width" | "height"> &
  Partial<
    Pick<
      PhotoSummary,
      "dominantColor" | "altText" | "title" | "availableWidths"
    >
  >;

interface PhotoMediaProps {
  photo: PhotoSource;
  imageBaseUrl: string;
  sizes: string;
  className?: string;
  imgClassName?: string;
  loading?: "lazy" | "eager";
  fetchPriority?: "high" | "low" | "auto";
  alt?: string;
}

/**
 * React's `srcSet` prop is emitted as `srcSet` by the server renderer. HTML
 * attribute names are case-insensitive, but view-source, crawlers and the
 * project's checks expect the canonical lowercase name. Production therefore
 * emits `srcset` directly; development keeps the recognised prop so React's
 * dev-only "unknown property" warning stays out of the server log.
 */
function srcsetAttribute(value: string) {
  return import.meta.env.PROD ? { srcset: value } : { srcSet: value };
}

/**
 * AVIF/WebP/JPEG `<picture>` built from whatever widths the API actually has.
 * Width, height and the dominant-colour plate keep the box stable before the
 * bytes arrive, so late image loads cannot shift the layout.
 */
export function PhotoMedia({
  photo,
  imageBaseUrl,
  sizes,
  className,
  imgClassName,
  loading = "lazy",
  fetchPriority = "auto",
  alt,
}: PhotoMediaProps) {
  const widths = photo.availableWidths
    ? normalizeWidths(photo.availableWidths)
    : DERIVATIVE_WIDTHS.filter((width) => width <= Number(photo.width));
  const fallbackWidth = pickWidth(widths, 1280) ?? widths.at(-1);
  const altText = alt ?? photoAltText(photo);
  const backgroundColor = photo.dominantColor;
  const jpegSrcSet = buildSrcSet(imageBaseUrl, photo.sha256, widths, "jpg");

  if (!fallbackWidth) {
    return (
      <div
        className={className}
        style={{ backgroundColor }}
        role="img"
        aria-label={altText}
      />
    );
  }

  return (
    <picture>
      {DERIVATIVE_FORMATS.map((format) => (
        <source
          key={format}
          type={FORMAT_MIME_TYPE[format]}
          {...srcsetAttribute(
            buildSrcSet(imageBaseUrl, photo.sha256, widths, format),
          )}
          sizes={sizes}
        />
      ))}
      <img
        src={derivativeUrl(imageBaseUrl, photo.sha256, fallbackWidth, "jpg")}
        {...srcsetAttribute(jpegSrcSet)}
        sizes={sizes}
        width={Number(photo.width)}
        height={Number(photo.height)}
        alt={altText}
        loading={loading}
        fetchPriority={fetchPriority}
        decoding="async"
        className={imgClassName}
        style={{ backgroundColor }}
      />
    </picture>
  );
}
