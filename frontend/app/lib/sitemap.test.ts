import { describe, expect, it } from "vitest";
import { absoluteUrl, escapeXml, sitemapXml } from "./sitemap";

describe("absoluteUrl", () => {
  it("joins paths without doubling slashes", () => {
    expect(absoluteUrl("https://example.test", "/photos/1")).toBe(
      "https://example.test/photos/1",
    );
    expect(absoluteUrl("https://example.test/", "/")).toBe(
      "https://example.test/",
    );
  });
});

describe("escapeXml", () => {
  it("escapes every XML metacharacter", () => {
    expect(escapeXml(`a&b<c>d"e'f`)).toBe(
      "a&amp;b&lt;c&gt;d&quot;e&apos;f",
    );
  });
});

describe("sitemapXml", () => {
  it("emits a urlset with locations and optional hints", () => {
    const xml = sitemapXml([
      { loc: "https://example.test/", changefreq: "daily", priority: "1.0" },
      { loc: "https://example.test/photos/1" },
    ]);

    expect(xml).toContain(
      '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    );
    expect(xml).toContain("<loc>https://example.test/</loc>");
    expect(xml).toContain("<changefreq>daily</changefreq>");
    expect(xml).toContain("<priority>1.0</priority>");
    expect(xml).toContain("<loc>https://example.test/photos/1</loc>");
    expect(xml).not.toContain("undefined");
  });
});
