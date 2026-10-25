import { useCallback, useState, type CSSProperties } from "react";
import { Link, useNavigate, useParams } from "react-router";
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
  rectSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical } from "lucide-react";
import {
  AdminApiError,
  adminKeys,
  editPhoto,
  getHome,
  getPhotoDetail,
  listCollectionPhotos,
  listCollections,
  reorderPhotos,
  uploadPhotos,
} from "~/api/admin";
import { ApiError } from "~/api/http";
import type { CollectionAdminView, PhotoSummary } from "~/api/generated/model";
import { Button } from "~/components/ui/button";
import {
  errorMessage,
  payloadFromPhotoDetail,
  uploadRejectionReason,
} from "~/lib/admin";
import {
  derivativeUrl,
  photoAltText,
  pickWidth,
  scaledHeight,
} from "~/lib/images";
import { useRootData } from "~/lib/root-data";

type UploadStatus = "queued" | "uploading" | "done" | "failed";

interface UploadRow {
  key: string;
  name: string;
  size: number;
  status: UploadStatus;
  progress: number;
  reason: string | null;
}

const INPUT =
  "border border-rule bg-bg px-2 py-1 text-xs outline-none focus-visible:border-accent";

function isUnauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}

function SortablePhotoCard({
  photo,
  index,
  featuredIndex,
  isFeaturePending,
  onToggleFeatured,
  onSetRank,
}: {
  photo: PhotoSummary;
  index: number;
  featuredIndex: number;
  isFeaturePending: boolean;
  onToggleFeatured: () => void;
  onSetRank: (rank: number) => void;
}) {
  const { imageBaseUrl } = useRootData();
  const [rankDraft, setRankDraft] = useState<string | null>(null);
  const [rankError, setRankError] = useState<string | null>(null);

  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: photo.id });

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const width = pickWidth(photo.availableWidths, 640) ?? 320;
  const title = photo.title?.trim() || "Untitled photograph";
  const rankValue =
    rankDraft ?? (featuredIndex >= 0 ? String(featuredIndex) : "0");

  return (
    <li
      ref={setNodeRef}
      style={style}
      className={`flex flex-col border border-rule bg-bg ${isDragging ? "opacity-60" : ""}`}
    >
      <div className="flex items-center justify-between border-b border-rule px-2 py-1">
        <button
          type="button"
          {...attributes}
          {...listeners}
          aria-label={`Reorder ${title}`}
          className="cursor-grab border border-transparent p-1 text-muted-foreground hover:border-rule hover:text-fg active:cursor-grabbing"
        >
          <GripVertical aria-hidden className="size-4" />
        </button>
        <span className="text-xs text-muted-foreground">
          {index + 1}
          {featuredIndex >= 0 ? ` · home ${featuredIndex + 1}` : ""}
        </span>
      </div>

      <Link
        to={`/admin/photos/${photo.id}`}
        className="block focus-visible:outline focus-visible:outline-2 focus-visible:outline-accent"
      >
        <img
          src={derivativeUrl(imageBaseUrl, photo.sha256, width, "jpg")}
          alt={photoAltText(photo)}
          width={width}
          height={scaledHeight(photo.width, photo.height, width)}
          loading="lazy"
          className="aspect-[4/3] w-full bg-surface object-cover"
        />
      </Link>

      <div className="flex flex-1 flex-col gap-2 p-3">
        <div className="min-w-0">
          <Link
            to={`/admin/photos/${photo.id}`}
            className="font-heading text-lg tracking-tight hover:text-accent"
          >
            {title}
          </Link>
          <p className="text-xs text-muted-foreground">
            {photo.width} × {photo.height}
            {photo.takenAt ? ` · ${photo.takenAt.slice(0, 10)}` : ""}
          </p>
        </div>

        <div className="mt-auto space-y-2">
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={isFeaturePending}
            onClick={onToggleFeatured}
          >
            {featuredIndex >= 0 ? "Unfeature" : "Feature"}
          </Button>

          {featuredIndex >= 0 ? (
            <form
              className="flex items-center gap-1"
              onSubmit={(event) => {
                event.preventDefault();
                const trimmed = rankValue.trim();
                if (!/^\d+$/.test(trimmed)) {
                  setRankError("Whole number, 0 or more.");
                  return;
                }
                setRankError(null);
                onSetRank(Number(trimmed));
              }}
            >
              <label
                htmlFor={`rank-${photo.id}`}
                className="text-xs text-muted-foreground"
              >
                Home rank
              </label>
              <input
                id={`rank-${photo.id}`}
                inputMode="numeric"
                value={rankValue}
                disabled={isFeaturePending}
                onChange={(event) => {
                  setRankDraft(event.target.value);
                  setRankError(null);
                }}
                className={`${INPUT} w-14`}
              />
              <Button
                type="submit"
                size="sm"
                variant="ghost"
                disabled={isFeaturePending}
              >
                Set
              </Button>
            </form>
          ) : null}
          {rankError ? (
            <p className="text-xs text-destructive" role="alert">
              {rankError}
            </p>
          ) : null}
        </div>
      </div>
    </li>
  );
}

export default function AdminCollection() {
  const params = useParams();
  const slug = params.slug ?? "";
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [uploadRows, setUploadRows] = useState<UploadRow[]>([]);
  const [uploading, setUploading] = useState(false);
  const [uploadNotice, setUploadNotice] = useState<string | null>(null);
  const [pageError, setPageError] = useState<string | null>(null);

  const collectionQuery = useQuery({
    queryKey: adminKeys.collection(slug),
    enabled: slug !== "",
    queryFn: async () => {
      const collections = await queryClient.fetchQuery({
        queryKey: adminKeys.collections,
        queryFn: listCollections,
        staleTime: 0,
      });
      const collection = collections.find(
        (candidate) => candidate.slug === slug,
      );
      if (!collection) {
        throw new AdminApiError(404, `No collection has the slug ${slug}.`);
      }
      const photos = collection.deletedAt
        ? []
        : await listCollectionPhotos(collection.slug);
      return { collection, photos };
    },
  });

  const home = useQuery({
    queryKey: adminKeys.home,
    queryFn: getHome,
  });

  const collection = collectionQuery.data?.collection;
  const photos = collectionQuery.data?.photos ?? [];
  const featured = home.data?.featured ?? [];

  const redirectIfUnauthorized = useCallback(
    (error: unknown): boolean => {
      if (!isUnauthorized(error)) {
        return false;
      }
      const returnTo = `${window.location.pathname}${window.location.search}`;
      void navigate(`/admin?returnTo=${encodeURIComponent(returnTo)}`, {
        replace: true,
      });
      return true;
    },
    [navigate],
  );

  const invalidate = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: adminKeys.collection(slug) }),
      queryClient.invalidateQueries({ queryKey: adminKeys.collections }),
      queryClient.invalidateQueries({ queryKey: adminKeys.home }),
    ]);
  }, [queryClient, slug]);

  const reorder = useMutation({
    mutationFn: (ids: string[]) => {
      if (!collection) {
        throw new Error("The collection has not loaded yet.");
      }
      return reorderPhotos(collection.id, ids);
    },
    onMutate: async (ids) => {
      setPageError(null);
      await queryClient.cancelQueries({
        queryKey: adminKeys.collection(slug),
      });
      const previous =
        queryClient.getQueryData<{
          collection: CollectionAdminView;
          photos: PhotoSummary[];
        }>(adminKeys.collection(slug));
      if (previous) {
        const byId = new Map(
          previous.photos.map((photo) => [photo.id, photo]),
        );
        const ordered = ids.flatMap((id) => {
          const photo = byId.get(id);
          return photo ? [photo] : [];
        });
        queryClient.setQueryData(adminKeys.collection(slug), {
          ...previous,
          photos: ordered,
        });
      }
      return { previous };
    },
    onError: (error, _ids, context) => {
      if (context?.previous) {
        queryClient.setQueryData(
          adminKeys.collection(slug),
          context.previous,
        );
      }
      if (!redirectIfUnauthorized(error)) {
        setPageError(`The new photo order was not saved: ${errorMessage(error)}`);
      }
    },
    onSettled: async () => {
      await invalidate();
    },
  });

  const feature = useMutation({
    mutationFn: async ({
      photo,
      rank,
    }: {
      photo: PhotoSummary;
      rank: number | null;
    }) => {
      if (!collection) {
        throw new Error("The collection has not loaded yet.");
      }
      const detail = await getPhotoDetail(photo.id);
      return editPhoto(
        photo.id,
        payloadFromPhotoDetail(detail, collection.id, rank),
      );
    },
    onSuccess: async () => {
      await invalidate();
    },
    onError: (error) => {
      if (!redirectIfUnauthorized(error)) {
        setPageError(errorMessage(error));
      }
    },
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) {
      return;
    }

    const oldIndex = photos.findIndex((photo) => photo.id === String(active.id));
    const newIndex = photos.findIndex((photo) => photo.id === String(over.id));
    if (oldIndex < 0 || newIndex < 0) {
      return;
    }

    const next = arrayMove(photos, oldIndex, newIndex);
    reorder.mutate(next.map((photo) => photo.id));
  }

  async function startUpload(files: File[]) {
    if (!collection || files.length === 0 || uploading) {
      return;
    }

    const rows: UploadRow[] = files.map((file) => ({
      key: crypto.randomUUID(),
      name: file.name,
      size: file.size,
      status: "queued",
      progress: 0,
      reason: null,
    }));

    setUploadRows(rows);
    setUploadNotice(null);
    setPageError(null);
    setUploading(true);

    let created = 0;
    let failed = 0;

    for (let index = 0; index < files.length; index += 1) {
      const file = files[index];
      const row = rows[index];
      if (!file || !row) {
        continue;
      }

      setUploadRows((current) =>
        current.map((candidate) =>
          candidate.key === row.key
            ? { ...candidate, status: "uploading" }
            : candidate,
        ),
      );

      try {
        // One request per file: exact per-file progress, and two uploads never
        // race to compute the same next position.
        const batch = await uploadPhotos(collection.id, [file], (progress) => {
          const percent =
            progress.total > 0
              ? Math.round((progress.loaded / progress.total) * 100)
              : 0;
          setUploadRows((current) =>
            current.map((candidate) =>
              candidate.key === row.key
                ? { ...candidate, progress: percent }
                : candidate,
            ),
          );
        });

        const failure = batch.failed.find(
          (candidate) => candidate.fileName === file.name,
        );
        if (failure) {
          failed += 1;
          setUploadRows((current) =>
            current.map((candidate) =>
              candidate.key === row.key
                ? {
                    ...candidate,
                    status: "failed",
                    progress: 100,
                    reason: uploadRejectionReason(failure.reason),
                  }
                : candidate,
            ),
          );
        } else {
          created += 1;
          setUploadRows((current) =>
            current.map((candidate) =>
              candidate.key === row.key
                ? { ...candidate, status: "done", progress: 100 }
                : candidate,
            ),
          );
        }
      } catch (error) {
        if (redirectIfUnauthorized(error)) {
          setUploading(false);
          return;
        }
        failed += 1;
        setUploadRows((current) =>
          current.map((candidate) =>
            candidate.key === row.key
              ? {
                  ...candidate,
                  status: "failed",
                  reason: errorMessage(error),
                }
              : candidate,
          ),
        );
      }
    }

    setUploading(false);
    setUploadNotice(
      `${created} added, ${failed} rejected.${
        created > 0 ? " The public site shows the new photographs on the next load." : ""
      }`,
    );
    await invalidate();
  }

  if (collectionQuery.isPending) {
    return <p className="text-sm text-muted-foreground">Loading collection…</p>;
  }

  if (collectionQuery.error) {
    return (
      <div className="max-w-xl">
        <h1 className="font-heading text-3xl tracking-tight">
          Collection not available
        </h1>
        <p className="mt-3 text-sm text-muted-foreground">
          {errorMessage(collectionQuery.error)}
        </p>
        <Link
          to="/admin/collections"
          className="mt-6 inline-block border-b border-accent pb-0.5 text-sm"
        >
          Back to collections
        </Link>
      </div>
    );
  }

  if (!collection) {
    return null;
  }

  return (
    <div className="space-y-10">
      <header>
        <Link
          to="/admin/collections"
          className="text-xs tracking-[0.2em] text-muted-foreground uppercase hover:text-fg"
        >
          Collections
        </Link>
        <div className="mt-3 flex flex-wrap items-baseline gap-x-5 gap-y-2">
          <h1 className="font-heading text-4xl tracking-tight">
            {collection.name}
          </h1>
          <span className="text-sm text-muted-foreground">
            /collections/{collection.slug}
          </span>
          {collection.deletedAt ? (
            <span className="border border-rule px-2 py-1 text-xs tracking-wide uppercase">
              Hidden
            </span>
          ) : null}
        </div>
        {collection.description ? (
          <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
            {collection.description}
          </p>
        ) : null}
      </header>

      {pageError ? (
        <p className="text-sm text-destructive" role="alert">
          {pageError}
        </p>
      ) : null}

      {!collection.deletedAt ? (
        <section className="border border-rule p-5" aria-labelledby="upload">
          <h2 id="upload" className="font-heading text-2xl tracking-tight">
            Upload photographs
          </h2>
          <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
            Files upload one at a time, so a rejected file never discards the
            rest. The API accepts images under 50 MB and at least 640 px on the
            long edge, up to 120 megapixels.
          </p>

          <div className="mt-4">
            <label
              htmlFor="upload-files"
              className="inline-block cursor-pointer border border-rule px-3 py-2 text-sm hover:border-accent"
            >
              {uploading ? "Uploading…" : "Choose files"}
            </label>
            <input
              id="upload-files"
              type="file"
              accept="image/*"
              multiple
              disabled={uploading}
              className="sr-only"
              onChange={(event) => {
                const input = event.currentTarget;
                const files = Array.from(input.files ?? []);
                input.value = "";
                void startUpload(files);
              }}
            />
          </div>

          {uploadRows.length > 0 ? (
            <div className="mt-5">
              <ul className="space-y-3">
                {uploadRows.map((row) => (
                  <li key={row.key} className="border-b border-rule pb-3">
                    <div className="flex flex-wrap items-baseline justify-between gap-2 text-sm">
                      <span className="min-w-0 break-all font-medium">
                        {row.name}
                      </span>
                      <span
                        className={
                          row.status === "failed"
                            ? "text-destructive"
                            : "text-muted-foreground"
                        }
                      >
                        {row.status === "queued"
                          ? "Waiting"
                          : row.status === "uploading"
                            ? `Uploading ${row.progress}%`
                            : row.status === "done"
                              ? "Added"
                              : "Rejected"}
                      </span>
                    </div>
                    {row.status === "uploading" || row.status === "queued" ? (
                      <div
                        className="mt-2 h-1 w-full bg-rule"
                        role="progressbar"
                        aria-label={`Upload progress for ${row.name}`}
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={row.progress}
                      >
                        <div
                          className="h-full bg-accent"
                          style={{ width: `${row.progress}%` }}
                        />
                      </div>
                    ) : null}
                    {row.reason ? (
                      <p className="mt-1 text-xs text-destructive" role="alert">
                        {row.reason}
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>

              {uploadNotice ? (
                <p className="mt-4 text-sm" role="status">
                  {uploadNotice}
                </p>
              ) : null}

              {!uploading ? (
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  className="mt-2"
                  onClick={() => {
                    setUploadRows([]);
                    setUploadNotice(null);
                  }}
                >
                  Clear upload list
                </Button>
              ) : null}
            </div>
          ) : null}
        </section>
      ) : (
        <p className="border border-rule bg-surface p-4 text-sm">
          This collection is hidden. Its photos do not appear on the public
          site, and the current API cannot unhide it.
        </p>
      )}

      <section aria-labelledby="photos">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <h2 id="photos" className="font-heading text-2xl tracking-tight">
            Photographs
          </h2>
          <p className="text-xs text-muted-foreground">
            Drag a handle to reorder. The first photograph is the collection
            cover.
          </p>
        </div>

        {photos.length === 0 ? (
          <p className="mt-4 text-sm text-muted-foreground">
            Nothing here yet. Upload the first photograph above.
          </p>
        ) : (
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
          >
            <SortableContext
              items={photos.map((photo) => photo.id)}
              strategy={rectSortingStrategy}
            >
              <ul className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {photos.map((photo, index) => {
                  const featuredIndex = featured.findIndex(
                    (candidate) => candidate.id === photo.id,
                  );
                  return (
                    <SortablePhotoCard
                      key={photo.id}
                      photo={photo}
                      index={index}
                      featuredIndex={featuredIndex}
                      isFeaturePending={
                        feature.isPending &&
                        feature.variables?.photo.id === photo.id
                      }
                      onToggleFeatured={() => {
                        feature.mutate({
                          photo,
                          rank:
                            featuredIndex >= 0 ? null : featured.length,
                        });
                      }}
                      onSetRank={(rank) => {
                        feature.mutate({ photo, rank });
                      }}
                    />
                  );
                })}
              </ul>
            </SortableContext>
          </DndContext>
        )}
      </section>
    </div>
  );
}
