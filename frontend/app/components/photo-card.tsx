import { Link } from "react-router";
import type { PhotoSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { GRID_SIZES, dimensionLabel } from "~/lib/images";
import { useRootData } from "~/lib/root-data";

interface PhotoCardProps {
  photo: PhotoSummary;
  index?: number;
  sizes?: string;
  priority?: boolean;
  onOpen?: (index: number, trigger: HTMLAnchorElement) => void;
}

export function PhotoCard({
  photo,
  index = 0,
  sizes = GRID_SIZES,
  priority = false,
  onOpen,
}: PhotoCardProps) {
  const { imageBaseUrl } = useRootData();
  const title = photo.title?.trim() || "Untitled photograph";

  return (
    <article className="border border-rule bg-surface">
      <Link
        to={`/photos/${photo.id}`}
        id={`photo-tile-${photo.id}`}
        className="group block focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
        onClick={(event) => {
          if (!onOpen) {
            return;
          }

          if (
            event.defaultPrevented ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey
          ) {
            return;
          }

          event.preventDefault();
          onOpen(index, event.currentTarget);
        }}
      >
        <div
          className="aspect-4/3 w-full overflow-hidden"
          style={{ backgroundColor: photo.dominantColor }}
        >
          <PhotoMedia
            photo={photo}
            imageBaseUrl={imageBaseUrl}
            sizes={sizes}
            loading={priority ? "eager" : "lazy"}
            fetchPriority={priority ? "high" : "auto"}
            imgClassName="h-full w-full object-cover"
          />
        </div>
        <div className="border-t border-rule px-3 py-3">
          <h2 className="font-heading text-lg leading-tight">{title}</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {dimensionLabel(photo.width, photo.height)}
          </p>
        </div>
      </Link>
    </article>
  );
}
