import { Link } from "react-router";
import type { CollectionSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { GRID_SIZES } from "~/lib/images";
import { useRootData } from "~/lib/root-data";

export function CollectionCard({
  collection,
  priority = false,
}: {
  collection: CollectionSummary;
  priority?: boolean;
}) {
  const { imageBaseUrl } = useRootData();

  return (
    <article className="border border-rule">
      <Link
        to={`/collections/${collection.slug}`}
        className="block focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
      >
        <div className="aspect-4/3 w-full overflow-hidden bg-surface">
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
