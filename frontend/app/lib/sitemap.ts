export function escapeXml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&apos;");
}

export function absoluteUrl(siteUrl: string, path: string): string {
  return `${siteUrl.replace(/\/$/, "")}${path.startsWith("/") ? path : `/${path}`}`;
}

export interface SitemapEntry {
  loc: string;
  changefreq?: "daily" | "weekly" | "monthly";
  priority?: string;
}

export function sitemapXml(entries: readonly SitemapEntry[]): string {
  const urls = entries
    .map(({ loc, changefreq, priority }) => {
      const parts = [`    <loc>${escapeXml(loc)}</loc>`];
      if (changefreq) {
        parts.push(`    <changefreq>${changefreq}</changefreq>`);
      }
      if (priority) {
        parts.push(`    <priority>${priority}</priority>`);
      }
      return `  <url>\n${parts.join("\n")}\n  </url>`;
    })
    .join("\n");

  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    urls,
    "</urlset>",
    "",
  ].join("\n");
}
