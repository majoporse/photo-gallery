import { site } from "~/content/site";

export function SiteFooter() {
  return (
    <footer className="mt-24 border-t border-rule">
      <div className="mx-auto max-w-6xl px-5 py-8 text-sm text-muted-foreground">
        <p className="sm:text-right">{site.name}</p>
      </div>
    </footer>
  );
}
