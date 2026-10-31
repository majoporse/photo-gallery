import type { Route } from "./+types/about";
import { site } from "~/content/site";

export const meta: Route.MetaFunction = () => [
  { title: `About ${site.name}` },
  {
    name: "description",
    content: `Who takes these photographs, and what he is looking for.`,
  },
  { property: "og:title", content: `About ${site.name}` },
  { property: "og:type", content: "website" },
  { name: "robots", content: "index, follow" },
];

export default function About() {
  return (
    <section className="mx-auto max-w-2xl px-5 py-16">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        About
      </p>
      <h1 className="mt-3 font-heading text-5xl tracking-tight sm:text-6xl">
        {site.name}
      </h1>
      {site.tagline ? (
        <p className="mt-4 font-heading text-2xl leading-snug text-muted-foreground">
          {site.tagline}
        </p>
      ) : null}

      <div className="mt-8 space-y-5 text-base leading-relaxed text-muted-foreground">
        {site.intro.map((paragraph, index) => (
          <p key={index}>{paragraph}</p>
        ))}
      </div>

      <div className="mt-12 border-t border-rule pt-6">
        <h2 className="font-heading text-xl tracking-tight">Contact</h2>
        <p className="mt-3">
          <a
            href={`mailto:${site.email}`}
            className="border-b border-accent pb-0.5 text-fg hover:text-accent"
          >
            {site.email}
          </a>
        </p>

        <ul className="mt-5 flex flex-wrap gap-x-5 gap-y-2 text-sm">
          <li>
            <a
              href={site.github.url}
              target="_blank"
              rel="noopener noreferrer"
              className="border-b border-rule pb-0.5 hover:border-accent hover:text-fg"
            >
              {site.github.label}
            </a>
          </li>
          {site.socials.map((social) => (
            <li key={social.url}>
              <a
                href={social.url}
                target="_blank"
                rel="noopener noreferrer"
                className="border-b border-rule pb-0.5 hover:border-accent hover:text-fg"
              >
                {social.label}
              </a>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
