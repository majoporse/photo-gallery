import { site } from "~/content/site";
import { useRootData } from "~/lib/root-data";

export function SiteFooter() {
  const { fixtures } = useRootData();

  return (
    <footer className="mt-24 border-t border-rule">
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-5 py-8 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <p>A personal photography archive.</p>

        <ul className="flex flex-wrap items-center gap-x-5 gap-y-2">
          <li>
            <a href={`mailto:${site.email}`} className="hover:text-fg">
              Email
            </a>
          </li>
          <li>
            <a
              href={site.github.url}
              target="_blank"
              rel="noopener noreferrer"
              className="hover:text-fg"
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
                className="hover:text-fg"
              >
                {social.label}
              </a>
            </li>
          ))}
        </ul>

        <p>
          {fixtures
            ? "Fixtures mode: sample data, no API."
            : "Photographs and text are the owner's."}
        </p>
      </div>
    </footer>
  );
}
