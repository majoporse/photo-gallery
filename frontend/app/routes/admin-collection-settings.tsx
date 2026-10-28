import { useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AdminApiError,
  adminKeys,
  deleteCollection,
  listCollections,
  restoreCollection,
  updateCollection,
} from "~/api/admin";
import type { CollectionAdminView } from "~/api/generated/model";
import { Button } from "~/components/ui/button";
import { errorMessage, liveCollections, textOrNull } from "~/lib/admin";

const INPUT =
  "mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm outline-none focus-visible:border-accent";
const LABEL = "text-xs tracking-[0.14em] text-muted-foreground uppercase";

function count(value: number | string): number {
  return Number(value);
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function CollectionSettings({
  collection,
  rank,
}: {
  collection: CollectionAdminView;
  rank: number;
}) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [name, setName] = useState(collection.name);
  const [description, setDescription] = useState(collection.description ?? "");
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmingHide, setConfirmingHide] = useState(false);

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: adminKeys.collections }),
      queryClient.invalidateQueries({
        queryKey: adminKeys.collection(collection.slug),
      }),
      queryClient.invalidateQueries({ queryKey: adminKeys.home }),
    ]);
  };

  const save = useMutation({
    mutationFn: () =>
      updateCollection(collection.id, {
        name: name.trim(),
        description: textOrNull(description),
      }),
    onSuccess: async () => {
      setNotice("Saved.");
      await invalidate();
    },
  });

  const hide = useMutation({
    mutationFn: () => deleteCollection(collection.id),
    onSuccess: async () => {
      await invalidate();
      void navigate("/admin/collections");
    },
  });

  const restore = useMutation({
    mutationFn: () => restoreCollection(collection.id),
    onSuccess: async () => {
      setNotice("Collection restored.");
      await invalidate();
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(null);
    save.mutate();
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
        <h1 className="mt-3 font-heading text-4xl tracking-tight">
          Collection settings
        </h1>
        <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
          Change how <strong>{collection.name}</strong> appears in the archive,
          or take it off the public site without deleting its photographs.
        </p>
      </header>

      <section className="border border-rule p-5" aria-labelledby="details">
        <h2 id="details" className="font-heading text-2xl tracking-tight">
          Details
        </h2>
        <form className="mt-4 grid gap-4 sm:grid-cols-2" onSubmit={submit}>
          <div>
            <label className={LABEL} htmlFor="collection-name">
              Name
            </label>
            <input
              id="collection-name"
              value={name}
              onChange={(event) => {
                setName(event.target.value);
                setNotice(null);
              }}
              required
              className={INPUT}
            />
          </div>
          <div>
            <span className={LABEL}>Slug (permanent)</span>
            <p className="mt-1 border border-rule bg-surface px-3 py-2 text-sm">
              <code>{collection.slug}</code>
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              The slug is the public URL and may be indexed, so it cannot be
              changed.
            </p>
          </div>
          <div className="sm:col-span-2">
            <label className={LABEL} htmlFor="collection-description">
              Description
            </label>
            <textarea
              id="collection-description"
              value={description}
              onChange={(event) => {
                setDescription(event.target.value);
                setNotice(null);
              }}
              rows={3}
              className={INPUT}
            />
          </div>
          <div className="flex flex-wrap items-center gap-4 sm:col-span-2">
            <Button
              type="submit"
              disabled={save.isPending || name.trim() === ""}
            >
              {save.isPending ? "Saving…" : "Save changes"}
            </Button>
            {notice ? (
              <p className="text-sm text-muted-foreground" role="status">
                {notice}
              </p>
            ) : null}
            {save.error ? (
              <p className="text-sm text-destructive" role="alert">
                {errorMessage(save.error)}
              </p>
            ) : null}
          </div>
        </form>
      </section>

      <section className="border border-rule p-5" aria-labelledby="visibility">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <h2 id="visibility" className="font-heading text-2xl tracking-tight">
            Visibility
          </h2>
          <span className="border border-rule px-2 py-1 text-xs tracking-wide uppercase">
            {collection.deletedAt ? "Hidden" : "Public"}
          </span>
        </div>

        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className={LABEL}>Position</dt>
            <dd className="mt-1">
              {collection.deletedAt
                ? "Returns to the end of the public order when restored."
                : rank >= 0
                  ? `${rank + 1} in the public order.`
                  : "Not in the public order."}{" "}
              <Link
                to="/admin/collections"
                className="border-b border-accent pb-0.5 text-xs"
              >
                Change order
              </Link>
            </dd>
          </div>
          <div>
            <dt className={LABEL}>Photographs</dt>
            <dd className="mt-1">
              <Link
                to={`/admin/collections/${collection.slug}`}
                className="border-b border-accent pb-0.5"
              >
                {count(collection.photoCount)} photo
                {count(collection.photoCount) === 1 ? "" : "s"}
              </Link>
            </dd>
          </div>
          <div>
            <dt className={LABEL}>Created</dt>
            <dd className="mt-1">{formatTimestamp(collection.createdAt)}</dd>
          </div>
          <div>
            <dt className={LABEL}>Updated</dt>
            <dd className="mt-1">{formatTimestamp(collection.updatedAt)}</dd>
          </div>
          <div>
            <dt className={LABEL}>Public page</dt>
            <dd className="mt-1">
              <Link
                to={`/collections/${collection.slug}`}
                className="border-b border-accent pb-0.5"
              >
                /collections/{collection.slug}
              </Link>
            </dd>
          </div>
        </dl>

        <div className="mt-5">
          {collection.deletedAt ? (
            <Button
              type="button"
              disabled={restore.isPending}
              onClick={() => {
                restore.mutate();
              }}
            >
              {restore.isPending ? "Restoring…" : "Restore collection"}
            </Button>
          ) : (
            <Button
              type="button"
              variant="destructive"
              onClick={() => {
                setConfirmingHide((value) => !value);
                hide.reset();
              }}
            >
              Hide collection…
            </Button>
          )}
          {restore.error ? (
            <p className="mt-3 text-sm text-destructive" role="alert">
              {restore.error instanceof AdminApiError &&
              restore.error.status === 409
                ? `Another live collection now uses /collections/${collection.slug}. Hide or rename it before restoring this one.`
                : errorMessage(restore.error)}
            </p>
          ) : null}
        </div>

        {confirmingHide ? (
          <div
            className="mt-4 border border-rule bg-surface p-4"
            role="alertdialog"
            aria-labelledby="confirm-hide"
          >
            <p id="confirm-hide" className="text-sm">
              Hide <strong>{collection.name}</strong> and its{" "}
              {count(collection.photoCount)} photo
              {count(collection.photoCount) === 1 ? "" : "s"} from the public
              site? The photos stay in storage, and this page can restore them
              later.
            </p>
            <div className="mt-3 flex gap-2">
              <Button
                type="button"
                size="sm"
                variant="destructive"
                disabled={hide.isPending}
                onClick={() => {
                  hide.mutate();
                }}
              >
                {hide.isPending ? "Hiding…" : "Hide collection"}
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => {
                  setConfirmingHide(false);
                }}
              >
                Keep it public
              </Button>
            </div>
            {hide.error ? (
              <p className="mt-3 text-sm text-destructive" role="alert">
                {errorMessage(hide.error)}
              </p>
            ) : null}
          </div>
        ) : null}
      </section>
    </div>
  );
}

export default function AdminCollectionSettingsPage() {
  const params = useParams();
  const slug = params.slug ?? "";
  const collections = useQuery({
    queryKey: adminKeys.collections,
    queryFn: listCollections,
    enabled: slug !== "",
  });

  if (collections.isPending) {
    return <p className="text-sm text-muted-foreground">Loading collection…</p>;
  }

  if (collections.error) {
    return (
      <p className="text-sm text-destructive" role="alert">
        {errorMessage(collections.error)}
      </p>
    );
  }

  const collection = collections.data?.find(
    (candidate) => candidate.slug === slug,
  );

  if (!collection) {
    return (
      <div className="max-w-xl">
        <h1 className="font-heading text-3xl tracking-tight">
          Collection not available
        </h1>
        <p className="mt-3 text-sm text-muted-foreground">
          No collection has the slug {slug}.
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

  const rank = liveCollections(collections.data ?? []).findIndex(
    (candidate) => candidate.id === collection.id,
  );

  return <CollectionSettings key={collection.id} collection={collection} rank={rank} />;
}
