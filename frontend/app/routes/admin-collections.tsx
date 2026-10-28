import { useState, type CSSProperties } from "react";
import { Link, useNavigate } from "react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical } from "lucide-react";
import {
  AdminApiError,
  adminKeys,
  createCollection,
  deleteCollection,
  listCollections,
  reorderCollections,
  restoreCollection,
} from "~/api/admin";
import type { CollectionAdminView } from "~/api/generated/model";
import { Button } from "~/components/ui/button";
import {
  errorMessage,
  hiddenCollections,
  isValidSlug,
  liveCollections,
  slugFromName,
  textOrNull,
} from "~/lib/admin";

const INPUT =
  "mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm outline-none focus-visible:border-accent";
const LABEL = "text-xs tracking-[0.14em] text-muted-foreground uppercase";

function count(value: number | string): number {
  return Number(value);
}

function SortableCollectionRow({
  collection,
}: {
  collection: CollectionAdminView;
}) {
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);

  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: collection.id });

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const remove = useMutation({
    mutationFn: () => deleteCollection(collection.id),
    onSuccess: async () => {
      setConfirming(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: adminKeys.collections }),
        queryClient.invalidateQueries({ queryKey: adminKeys.home }),
      ]);
    },
  });

  return (
    <li
      ref={setNodeRef}
      style={style}
      className={`border-b border-rule ${isDragging ? "bg-surface" : ""}`}
    >
      <div className="flex items-start gap-3 py-4">
        <button
          type="button"
          {...attributes}
          {...listeners}
          aria-label={`Reorder ${collection.name}`}
          className="mt-1 cursor-grab border border-transparent p-1 text-muted-foreground hover:border-rule hover:text-fg active:cursor-grabbing"
        >
          <GripVertical aria-hidden className="size-4" />
        </button>

        <div className="min-w-0 flex-1">
          <div>
            <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              <Link
                to={`/admin/collections/${collection.slug}`}
                className="font-heading text-xl tracking-tight hover:text-accent"
              >
                {collection.name}
              </Link>
              <span className="text-xs text-muted-foreground">
                /collections/{collection.slug}
              </span>
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              {count(collection.photoCount)} photo
              {count(collection.photoCount) === 1 ? "" : "s"}
              {collection.description ? ` — ${collection.description}` : ""}
            </p>
          </div>
        </div>

        <div className="flex shrink-0 gap-2">
          <Button asChild size="sm" variant="outline">
            <Link to={`/admin/collections/${collection.slug}/edit`}>Edit</Link>
          </Button>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => {
              setConfirming((value) => !value);
            }}
          >
            Hide…
          </Button>
        </div>
      </div>

      {confirming ? (
        <div
          className="mb-4 border border-rule bg-surface p-4"
          role="alertdialog"
          aria-labelledby={`confirm-${collection.id}`}
        >
          <p id={`confirm-${collection.id}`} className="text-sm">
            Hide <strong>{collection.name}</strong> and its{" "}
            {count(collection.photoCount)} photo
            {count(collection.photoCount) === 1 ? "" : "s"} from the public
            site? The photos are hidden, not deleted from storage. You can
            restore the collection from Hidden collections or its settings
            page.
          </p>
          <div className="mt-3 flex gap-2">
            <Button
              type="button"
              size="sm"
              variant="destructive"
              disabled={remove.isPending}
              onClick={() => {
                remove.mutate();
              }}
            >
              {remove.isPending ? "Hiding…" : "Hide collection"}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="ghost"
              onClick={() => {
                setConfirming(false);
                remove.reset();
              }}
            >
              Keep it visible
            </Button>
          </div>
          {remove.error ? (
            <p className="mt-3 text-sm text-destructive" role="alert">
              {errorMessage(remove.error)}
            </p>
          ) : null}
        </div>
      ) : null}
    </li>
  );
}

function HiddenCollectionRow({
  collection,
}: {
  collection: CollectionAdminView;
}) {
  const queryClient = useQueryClient();
  const restore = useMutation({
    mutationFn: () => restoreCollection(collection.id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: adminKeys.collections }),
        queryClient.invalidateQueries({ queryKey: adminKeys.home }),
      ]);
    },
  });

  return (
    <li className="flex flex-wrap items-start justify-between gap-4 border-b border-rule py-4">
      <div>
        <p className="font-heading text-lg tracking-tight text-muted-foreground">
          {collection.name}
        </p>
        <p className="text-xs text-muted-foreground">
          /collections/{collection.slug} · {count(collection.photoCount)} photo
          {count(collection.photoCount) === 1 ? "" : "s"}
        </p>
      </div>
      <div className="flex shrink-0 gap-2">
        <Button asChild size="sm" variant="outline">
          <Link to={`/admin/collections/${collection.slug}/edit`}>Edit</Link>
        </Button>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={restore.isPending}
          onClick={() => {
            restore.mutate();
          }}
        >
          {restore.isPending ? "Restoring…" : "Restore"}
        </Button>
      </div>
      {restore.error ? (
        <p className="w-full text-sm text-destructive" role="alert">
          {restore.error instanceof AdminApiError &&
          restore.error.status === 409
            ? `Another live collection now uses /collections/${collection.slug}. Hide or rename it before restoring this one.`
            : errorMessage(restore.error)}
        </p>
      ) : null}
    </li>
  );
}

export default function AdminCollections() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const collections = useQuery({
    queryKey: adminKeys.collections,
    queryFn: listCollections,
  });

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [slugEdited, setSlugEdited] = useState(false);
  const [description, setDescription] = useState("");
  const [orderError, setOrderError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: createCollection,
    onSuccess: async (collection) => {
      setName("");
      setSlug("");
      setSlugEdited(false);
      setDescription("");
      await queryClient.invalidateQueries({ queryKey: adminKeys.collections });
      void navigate(`/admin/collections/${collection.slug}`);
    },
  });

  const reorder = useMutation({
    mutationFn: reorderCollections,
    onMutate: async (ids) => {
      setOrderError(null);
      await queryClient.cancelQueries({ queryKey: adminKeys.collections });
      const previous = queryClient.getQueryData<CollectionAdminView[]>(
        adminKeys.collections,
      );
      if (previous) {
        const byId = new Map(
          previous.map((collection) => [collection.id, collection]),
        );
        const nextLive: CollectionAdminView[] = [];
        ids.forEach((id, index) => {
          const collection = byId.get(id);
          if (collection) {
            nextLive.push({ ...collection, position: index });
          }
        });
        queryClient.setQueryData(adminKeys.collections, [
          ...nextLive,
          ...hiddenCollections(previous),
        ]);
      }
      return { previous };
    },
    onError: (error, _ids, context) => {
      if (context?.previous) {
        queryClient.setQueryData(adminKeys.collections, context.previous);
      }
      setOrderError(errorMessage(error));
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: adminKeys.collections });
    },
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const live = liveCollections(collections.data ?? []);
  const hidden = hiddenCollections(collections.data ?? []);

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) {
      return;
    }

    const oldIndex = live.findIndex(
      (collection) => collection.id === String(active.id),
    );
    const newIndex = live.findIndex(
      (collection) => collection.id === String(over.id),
    );
    if (oldIndex < 0 || newIndex < 0) {
      return;
    }

    const next = arrayMove(live, oldIndex, newIndex);
    reorder.mutate(next.map((collection) => collection.id));
  }

  const slugValid = isValidSlug(slug.trim());
  const createError = create.error;
  const slugProblem =
    createError instanceof AdminApiError
      ? createError.errors.slug?.join(" ")
      : undefined;

  return (
    <div className="space-y-10">
      <header>
        <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
          Collections
        </p>
        <h1 className="mt-2 font-heading text-4xl tracking-tight">
          The archive
        </h1>
        <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
          Drag the handle to change the public order. Open a collection&apos;s
          settings to change its name, description or visibility. A
          collection&apos;s slug becomes its public URL and is permanent.
        </p>
      </header>

      <section className="border border-rule p-5">
        <h2 className="font-heading text-2xl tracking-tight">
          New collection
        </h2>
        <form
          className="mt-4 grid gap-4 sm:grid-cols-2"
          onSubmit={(event) => {
            event.preventDefault();
            create.mutate({
              name: name.trim(),
              slug: slug.trim(),
              description: textOrNull(description),
            });
          }}
        >
          <div>
            <label className={LABEL} htmlFor="new-name">
              Name
            </label>
            <input
              id="new-name"
              value={name}
              onChange={(event) => {
                const value = event.target.value;
                setName(value);
                if (!slugEdited) {
                  setSlug(slugFromName(value));
                }
              }}
              required
              className={INPUT}
            />
          </div>
          <div>
            <label className={LABEL} htmlFor="new-slug">
              Slug (permanent)
            </label>
            <input
              id="new-slug"
              value={slug}
              onChange={(event) => {
                setSlug(event.target.value);
                setSlugEdited(true);
              }}
              required
              spellCheck={false}
              aria-describedby="slug-help"
              className={INPUT}
            />
            <p
              id="slug-help"
              className={`mt-1 text-xs ${slug && !slugValid ? "text-destructive" : "text-muted-foreground"}`}
            >
              Lowercase letters, digits and single hyphens. It cannot be changed
              after the collection exists.
            </p>
          </div>
          <div className="sm:col-span-2">
            <label className={LABEL} htmlFor="new-description">
              Description
            </label>
            <textarea
              id="new-description"
              value={description}
              onChange={(event) => {
                setDescription(event.target.value);
              }}
              rows={2}
              className={INPUT}
            />
          </div>
          <div className="sm:col-span-2">
            <Button
              type="submit"
              disabled={create.isPending || !slugValid || name.trim() === ""}
            >
              {create.isPending ? "Creating…" : "Create collection"}
            </Button>
          </div>
          {createError ? (
            <p className="text-sm text-destructive sm:col-span-2" role="alert">
              {slugProblem ?? errorMessage(createError)}
            </p>
          ) : null}
        </form>
      </section>

      <section aria-labelledby="live-collections">
        <h2 id="live-collections" className="font-heading text-2xl tracking-tight">
          Public collections
        </h2>

        {collections.isPending ? (
          <p className="mt-4 text-sm text-muted-foreground">Loading…</p>
        ) : collections.error ? (
          <p className="mt-4 text-sm text-destructive" role="alert">
            {errorMessage(collections.error)}
          </p>
        ) : live.length === 0 ? (
          <p className="mt-4 text-sm text-muted-foreground">
            No public collections yet. Create the first one above.
          </p>
        ) : (
          <>
            {orderError ? (
              <p className="mt-4 text-sm text-destructive" role="alert">
                The new order was not saved: {orderError}
              </p>
            ) : null}
            <DndContext
              sensors={sensors}
              collisionDetection={closestCenter}
              onDragEnd={handleDragEnd}
            >
              <SortableContext
                items={live.map((collection) => collection.id)}
                strategy={verticalListSortingStrategy}
              >
                <ul className="mt-2">
                  {live.map((collection) => (
                    <SortableCollectionRow
                      key={collection.id}
                      collection={collection}
                    />
                  ))}
                </ul>
              </SortableContext>
            </DndContext>
          </>
        )}
      </section>

      {hidden.length > 0 ? (
        <section aria-labelledby="hidden-collections">
          <h2
            id="hidden-collections"
            className="font-heading text-2xl tracking-tight"
          >
            Hidden collections
          </h2>
          <p className="mt-2 text-sm text-muted-foreground">
            Hidden collections are off the public site but not erased: the
            collection and its photos remain in storage. Restoring brings back
            the photographs that were visible when the collection was hidden.
          </p>
          <ul className="mt-2">
            {hidden.map((collection) => (
              <HiddenCollectionRow
                key={collection.id}
                collection={collection}
              />
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
