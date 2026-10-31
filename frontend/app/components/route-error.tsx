import { isRouteErrorResponse, Link, useRouteError } from "react-router";

export function NotFound() {
  return (
    <section className="mx-auto max-w-2xl px-5 py-24">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        404
      </p>
      <h1 className="mt-3 font-heading text-4xl tracking-tight">
        This one came back blank
      </h1>
      <p className="mt-4 text-muted-foreground">
        The collection or photograph you asked for is not in the archive — it
        may have been removed, or the link may never have been right. Blank
        frames happen. Everything that is published is still on the contact
        sheet, one link away.
      </p>
      <div className="mt-8 flex flex-wrap items-center gap-5 text-sm">
        <Link to="/collections" className="border-b border-accent pb-0.5">
          Browse collections
        </Link>
        <Link to="/" className="border-b border-accent pb-0.5">
          Return to the gallery
        </Link>
      </div>
    </section>
  );
}

export function RouteError() {
  const error = useRouteError();

  if (isRouteErrorResponse(error) && error.status === 404) {
    return <NotFound />;
  }

  const detail =
    import.meta.env.DEV && error instanceof Error
      ? error.message
      : "The page could not be rendered. Please try again.";

  return (
    <section className="mx-auto max-w-2xl px-5 py-24">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        Error
      </p>
      <h1 className="mt-3 font-heading text-4xl tracking-tight">
        Something went wrong
      </h1>
      <p className="mt-4 text-muted-foreground">{detail}</p>
      <Link
        to="/"
        className="mt-8 inline-block border-b border-accent pb-0.5 text-sm"
      >
        Return to the gallery
      </Link>
    </section>
  );
}
