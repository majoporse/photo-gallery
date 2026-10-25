import { useCallback, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  adminKeys,
  deletePhoto,
  editPhoto,
  getHome,
  getPhotoDetail,
  listCollections,
} from "~/api/admin";
import { ApiError } from "~/api/http";
import type {
  CollectionAdminView,
  HomeFeed,
  PhotoDetail,
} from "~/api/generated/model";
import { Button } from "~/components/ui/button";
import {
  buildPhotoEditPayload,
  errorMessage,
  formatDateTimeLocal,
  type PhotoFormState,
} from "~/lib/admin";

const INPUT =
  "mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm outline-none focus-visible:border-accent";
const LABEL = "text-xs tracking-[0.14em] text-muted-foreground uppercase";

interface PhotoDraft {
  detail: PhotoDetail;
  collections: CollectionAdminView[];
  collection: CollectionAdminView;
  featuredIndex: number;
  home: HomeFeed;
}

function PhotoForm({ draft }: { draft: PhotoDraft }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { detail, collections, collection, featuredIndex, home } = draft;

  const [title, setTitle] = useState(detail.photo.title ?? "");
  const [caption, setCaption] = useState(detail.caption ?? "");
  const [altText, setAltText] = useState(detail.photo.altText ?? "");
  const [takenAt, setTakenAt] = useState(
    formatDateTimeLocal(detail.photo.takenAt),
  );
  const [collectionId, setCollectionId] = useState(collection.id);
  const [featured, setFeatured] = useState(featuredIndex >= 0);
  const [featuredRank, setFeaturedRank] = useState(
    featuredIndex >= 0 ? String(featuredIndex) : String(home.featured.length),
  );
  const [formError, setFormError] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const redirectIfUnauthorized = useCallback(
    (error: unknown): boolean => {
      if (!(error instanceof ApiError) || error.status !== 401) {
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

  const destination = collections.find(
    (candidate) => candidate.id === collectionId,
  );
  const moved = destination !== undefined && destination.id !== collection.id;

  const invalidate = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: adminKeys.photo(detail.photo.id) }),
      queryClient.invalidateQueries({
        queryKey: adminKeys.collection(detail.collectionSlug),
      }),
      queryClient.invalidateQueries({ queryKey: adminKeys.collections }),
      queryClient.invalidateQueries({ queryKey: adminKeys.home }),
      ...(destination
        ? [
            queryClient.invalidateQueries({
              queryKey: adminKeys.collection(destination.slug),
            }),
          ]
        : []),
    ]);
  }, [
    destination,
    detail.collectionSlug,
    detail.photo.id,
    queryClient,
  ]);

  const save = useMutation({
    mutationFn: (state: PhotoFormState) => {
      const result = buildPhotoEditPayload(state);
      if (!result.ok) {
        throw new Error(result.error);
      }
      return editPhoto(detail.photo.id, result.payload);
    },
    onSuccess: async () => {
      setFormError(null);
      setSaved(
        moved && destination
          ? `Saved. Moved to ${destination.name}, at the end of its photographs.`
          : "Saved. The public site uses the new values on its next load.",
      );
      await invalidate();
    },
    onError: (error) => {
      setSaved(null);
      if (!redirectIfUnauthorized(error)) {
        setFormError(errorMessage(error));
      }
    },
  });

  const remove = useMutation({
    mutationFn: () => deletePhoto(detail.photo.id),
    onSuccess: async () => {
      await invalidate();
      void navigate(`/admin/collections/${detail.collectionSlug}`);
    },
    onError: (error) => {
      if (!redirectIfUnauthorized(error)) {
        setFormError(errorMessage(error));
      }
    },
  });

  const featuredCount = home.featured.length;

  return (
    <div className="space-y-8">
      <header>
        <Link
          to={`/admin/collections/${detail.collectionSlug}`}
          className="text-xs tracking-[0.2em] text-muted-foreground uppercase hover:text-fg"
        >
          {detail.collectionName}
        </Link>
        <h1 className="mt-3 font-heading text-4xl tracking-tight">
          {detail.photo.title?.trim() || "Untitled photograph"}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Photo {detail.photo.id} · {detail.photo.width} × {detail.photo.height}
        </p>
      </header>

      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <form
          className="space-y-6"
          onSubmit={(event) => {
            event.preventDefault();
            setSaved(null);
            save.mutate({
              title,
              caption,
              altText,
              takenAt,
              featured,
              featuredRank,
              collectionId,
            });
          }}
        >
          <div>
            <label className={LABEL} htmlFor="photo-title">
              Title
            </label>
            <input
              id="photo-title"
              value={title}
              onChange={(event) => {
                setTitle(event.target.value);
              }}
              className={INPUT}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Empty becomes “Untitled photograph” on the public site.
            </p>
          </div>

          <div>
            <label className={LABEL} htmlFor="photo-caption">
              Caption
            </label>
            <textarea
              id="photo-caption"
              value={caption}
              onChange={(event) => {
                setCaption(event.target.value);
              }}
              rows={3}
              className={INPUT}
            />
          </div>

          <div>
            <label className={LABEL} htmlFor="photo-alt">
              Alt text
            </label>
            <input
              id="photo-alt"
              value={altText}
              onChange={(event) => {
                setAltText(event.target.value);
              }}
              className={INPUT}
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Used by screen readers and social previews. Falls back to the
              title when empty.
            </p>
          </div>

          <div className="grid gap-6 sm:grid-cols-2">
            <div>
              <label className={LABEL} htmlFor="photo-taken-at">
                Taken at
              </label>
              <input
                id="photo-taken-at"
                type="datetime-local"
                value={takenAt}
                onChange={(event) => {
                  setTakenAt(event.target.value);
                }}
                className={INPUT}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Read as your local time and stored as UTC. Leaving it empty
                clears it.
              </p>
            </div>

            <div>
              <label className={LABEL} htmlFor="photo-collection">
                Collection
              </label>
              <select
                id="photo-collection"
                value={collectionId}
                onChange={(event) => {
                  setCollectionId(event.target.value);
                }}
                className={INPUT}
              >
                {collections.map((candidate) => (
                  <option key={candidate.id} value={candidate.id}>
                    {candidate.name}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-xs text-muted-foreground">
                Moving appends the photo to the end of the destination
                collection.
              </p>
            </div>
          </div>

          <fieldset className="border border-rule p-4">
            <legend className="px-1 text-xs tracking-[0.14em] text-muted-foreground uppercase">
              Home page
            </legend>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={featured}
                onChange={(event) => {
                  setFeatured(event.target.checked);
                }}
              />
              Featured on the home page
            </label>

            {featured ? (
              <div className="mt-4 max-w-40">
                <label className={LABEL} htmlFor="photo-rank">
                  Featured rank
                </label>
                <input
                  id="photo-rank"
                  inputMode="numeric"
                  value={featuredRank}
                  onChange={(event) => {
                    setFeaturedRank(event.target.value);
                  }}
                  className={INPUT}
                />
                <p className="mt-1 text-xs text-muted-foreground">
                  Lower comes first. {featuredCount} photo
                  {featuredCount === 1 ? "" : "s"} currently featured.
                </p>
              </div>
            ) : null}

            <p className="mt-4 text-xs text-muted-foreground">
              The API does not return the stored rank. When this photo is
              already featured, the number above is its current position in the
              home feed.
            </p>
          </fieldset>

          <div className="flex flex-wrap items-center gap-3">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? "Saving…" : "Save changes"}
            </Button>
            <Link
              to={`/admin/collections/${detail.collectionSlug}`}
              className="text-sm text-muted-foreground underline-offset-4 hover:text-fg hover:underline"
            >
              Back to {detail.collectionName}
            </Link>
          </div>

          {formError ? (
            <p className="text-sm text-destructive" role="alert">
              {formError}
            </p>
          ) : null}
          {saved ? (
            <p className="text-sm" role="status">
              {saved}
            </p>
          ) : null}
        </form>

        <aside className="space-y-8">
          <div className="border border-rule p-4">
            <h2 className="font-heading text-xl tracking-tight">
              Hide this photo
            </h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Hiding removes the photograph from the public site and from its
              collection. The file stays in storage, but this screen cannot
              restore it.
            </p>
            {confirmingDelete ? (
              <div
                className="mt-4 border border-rule bg-surface p-3"
                role="alertdialog"
                aria-labelledby="confirm-photo-delete"
              >
                <p id="confirm-photo-delete" className="text-sm">
                  Hide this photo? It leaves the collection immediately and
                  cannot be brought back from this UI.
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
                    variant="destructive"
                    disabled={remove.isPending}
                    onClick={() => {
                      remove.mutate();
                    }}
                  >
                    {remove.isPending ? "Hiding…" : "Hide photo"}
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      setConfirmingDelete(false);
                    }}
                  >
                    Keep it visible
                  </Button>
                </div>
              </div>
            ) : (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="mt-4"
                onClick={() => {
                  setConfirmingDelete(true);
                }}
              >
                Hide photo…
              </Button>
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}

export default function AdminPhoto() {
  const params = useParams();
  const photoId = params.id ?? "";
  const queryClient = useQueryClient();

  const photoQuery = useQuery({
    queryKey: adminKeys.photo(photoId),
    enabled: photoId !== "",
    queryFn: async () => {
      const [detail, home, collections] = await Promise.all([
        getPhotoDetail(photoId),
        getHome(),
        queryClient.fetchQuery({
          queryKey: adminKeys.collections,
          queryFn: listCollections,
          staleTime: 0,
        }),
      ]);

      const collection = collections.find(
        (candidate) => candidate.slug === detail.collectionSlug,
      );
      if (!collection) {
        throw new Error("The photo's collection is not in the admin list.");
      }

      return {
        detail,
        home,
        collections: collections.filter(
          (candidate) => candidate.deletedAt === null,
        ),
        collection,
        featuredIndex: home.featured.findIndex(
          (candidate) => candidate.id === detail.photo.id,
        ),
      } satisfies PhotoDraft;
    },
  });

  if (photoQuery.isPending) {
    return <p className="text-sm text-muted-foreground">Loading photo…</p>;
  }

  if (photoQuery.error) {
    return (
      <div className="max-w-xl">
        <h1 className="font-heading text-3xl tracking-tight">
          Photo not available
        </h1>
        <p className="mt-3 text-sm text-muted-foreground">
          {errorMessage(photoQuery.error)}
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

  return <PhotoForm draft={photoQuery.data} />;
}
