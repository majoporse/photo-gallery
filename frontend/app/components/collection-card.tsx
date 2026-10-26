import { Link } from "react-router";
import type { CollectionSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { GRID_SIZES } from "~/lib/images";
import { useRootData } from "~/lib/root-data";
import { cn } from "~/lib/utils";

export function CollectionCard({
  collection,
  priority = false,
  naturalAspect = false,
}: {
  collection: CollectionSummary;
  priority?: boolean;
  naturalAspect?: boolean;
}) {
  const { imageBaseUrl } = useRootData();
  const aspectRatio =
    naturalAspect && collection.cover
      ? `${collection.cover.width} / ${collection.cover.height}`
      : undefined;

  return (
    <article className="border border-rule">
      <Link
        to={`/collections/${collection.slug}`}
        className="block focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
      >
        <div
          className={cn(
            "w-full overflow-hidden bg-surface",
            aspectRatio ? undefined : "aspect-4/3",
          )}
          style={{ aspectRatio }}
        >
          {collection.cover ? (
            <PhotoMedia
              photo={collection.cover}
              imageBaseUrl={imageBaseUrl}
              sizes={GRID_SIZES}
              loading={priority ? "eager" : "lazy"}
              fetchPriority={priority ? "high" : "auto"}
              alt={`Cover photograph for ${collection.name}`}
              imgClassName="h-full w-full object-cover"
            />
          ) : null}
        </div>
        <div className="border-t border-rule px-3 py-3">
          <h2 className="font-heading text-xl leading-tight">
            {collection.name}
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {collection.photoCount} photographs
          </p>
        </div>
      </Link>
    </article>
  );
}
