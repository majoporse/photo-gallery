import { Link } from "react-router";
import type { PhotoSummary } from "~/api/generated/model";
import { dimensionLabel } from "~/lib/images";

export function PhotoCard({ photo }: { photo: PhotoSummary }) {
  const title = photo.title ?? "Untitled";

  return (
    <article className="border border-rule">
      <Link
        to={`/photos/${photo.id}`}
        className="block focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
      >
        <div
          className="aspect-4/3 w-full"
          style={{ backgroundColor: photo.dominantColor }}
          role="img"
          aria-label={photo.altText ?? title}
        />
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
