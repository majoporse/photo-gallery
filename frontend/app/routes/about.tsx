import type { Route } from "./+types/about";

export const meta: Route.MetaFunction = () => [
  { title: "About — Gallery" },
  {
    name: "description",
    content: "About this archive, and why it is arranged the way it is.",
  },
  { property: "og:title", content: "About — Gallery" },
  { property: "og:type", content: "website" },
  { name: "robots", content: "index, follow" },
];

export default function About() {
  return (
    <section className="mx-auto max-w-2xl px-5 py-16">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        About
      </p>
      <h1 className="mt-3 font-heading text-5xl tracking-tight">
        A quiet place for photographs.
      </h1>

      <div className="mt-8 space-y-5 text-base leading-relaxed text-muted-foreground">
        <p>
          This is a personal archive: one owner, a few collections, and no
          hurry. Photographs are grouped by the place or subject that produced
          them, then left alone. Collections do not change their addresses, so
          links keep working.
        </p>
        <p>
          There are no accounts, comments, likes or search. The point is to
          look at the pictures. Everything here is served as a static image
          derivative at one of four widths, chosen by the browser.
        </p>
        <p>
          The archive is built as a server-rendered site so that each photograph
          has a real page, a real title, and a useful preview card when someone
          shares it.
        </p>
      </div>
    </section>
  );
}
