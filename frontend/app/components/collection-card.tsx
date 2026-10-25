import { Link } from "react-router";
import type { CollectionSummary } from "~/api/generated/model";

export function CollectionCard({
  collection,
}: {
  collection: CollectionSummary;
}) {
  return (
    <article className="border border-rule">
      <Link
        to={`/collections/${collection.slug}`}
        className="block focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
      >
        <div className="aspect-[4/3] w-full bg-surface" aria-hidden="true" />
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
