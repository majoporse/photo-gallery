import Lightbox, { type Plugin, type Slide } from "yet-another-react-lightbox";
import Captions from "yet-another-react-lightbox/plugins/captions";
import Thumbnails from "yet-another-react-lightbox/plugins/thumbnails";
import Zoom from "yet-another-react-lightbox/plugins/zoom";
import "yet-another-react-lightbox/styles.css";
import "yet-another-react-lightbox/plugins/captions.css";
import "yet-another-react-lightbox/plugins/thumbnails.css";

import type { PhotoSummary } from "~/api/generated/model";
import {
  derivativeUrl,
  normalizeWidths,
  photoAltText,
  preferredWidth,
  scaledHeight,
} from "~/lib/images";
import { useMounted } from "~/lib/use-mounted";

const PLUGINS: Plugin[] = [Zoom, Captions, Thumbnails];

interface PhotoLightboxProps {
  photos: readonly PhotoSummary[];
  imageBaseUrl: string;
  index: number | null;
  collectionName: string;
  onClose: () => void;
}

function toSlides(
  photos: readonly PhotoSummary[],
  imageBaseUrl: string,
  collectionName: string,
): Slide[] {
  return photos.map((photo) => {
    const widths = normalizeWidths(photo.availableWidths);
    const largest = preferredWidth(widths) ?? widths.at(-1);
    const src = largest
      ? derivativeUrl(imageBaseUrl, photo.sha256, largest, "jpg")
      : "";

    return {
      src,
      alt: photoAltText(photo),
      width: largest ?? Number(photo.width),
      height: largest
        ? scaledHeight(photo.width, photo.height, largest)
        : Number(photo.height),
      title: photo.title?.trim() || "Untitled photograph",
      description: photo.altText?.trim() || collectionName,
      thumbnail: widths[0]
        ? derivativeUrl(imageBaseUrl, photo.sha256, widths[0], "jpg")
        : src,
      srcSet: widths.map((width) => ({
        src: derivativeUrl(imageBaseUrl, photo.sha256, width, "jpg"),
        width,
        height: scaledHeight(photo.width, photo.height, width),
      })),
    };
  });
}

/**
 * Client-only viewer. Mount gating means the server HTML (and the hydration
 * pass) contains no lightbox markup; keyboard handling and the focus trap live
 * inside the library, and closing returns focus to the tile that opened it.
 */
export function PhotoLightbox({
  photos,
  imageBaseUrl,
  index,
  collectionName,
  onClose,
}: PhotoLightboxProps) {
  const mounted = useMounted();

  if (!mounted || index === null || photos.length === 0) {
    return null;
  }

  return (
    <Lightbox
      open
      close={onClose}
      index={index}
      slides={toSlides(photos, imageBaseUrl, collectionName)}
      plugins={PLUGINS}
      labels={{ Lightbox: "Photograph viewer" }}
      carousel={{ finite: true, imageFit: "contain" }}
      thumbnails={{
        position: "bottom",
        width: 80,
        height: 60,
        gap: 8,
        showToggle: true,
      }}
      captions={{ showToggle: true, descriptionTextAlign: "center" }}
      zoom={{ maxZoomPixelRatio: 3 }}
    />
  );
}
