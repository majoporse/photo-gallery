import { getSiteUrl } from "~/lib/env.server";

export function loader() {
  const body = [
    "User-agent: *",
    "Disallow: /admin",
    "Allow: /",
    "",
    `Sitemap: ${getSiteUrl()}/sitemap.xml`,
    "",
  ].join("\n");

  return new Response(body, {
    headers: {
      "Content-Type": "text/plain; charset=utf-8",
      "Cache-Control": "public, max-age=3600",
    },
  });
}
