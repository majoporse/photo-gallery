import { describe, expect, it } from "vitest";
import {
  DEFAULT_THEME,
  isTheme,
  parseTheme,
  THEME_INIT_SCRIPT,
  themeFromCookieHeader,
} from "./theme";

describe("parseTheme", () => {
  it("accepts the three known themes", () => {
    expect(parseTheme("paper")).toBe("paper");
    expect(parseTheme("ink")).toBe("ink");
    expect(parseTheme("grey")).toBe("grey");
  });

  it("falls back for missing, unknown or malformed values", () => {
    expect(parseTheme(undefined)).toBe(DEFAULT_THEME);
    expect(parseTheme(null)).toBe(DEFAULT_THEME);
    expect(parseTheme("")).toBe(DEFAULT_THEME);
    expect(parseTheme("dark")).toBe(DEFAULT_THEME);
    expect(parseTheme("INK")).toBe(DEFAULT_THEME);
  });

  it("narrows with isTheme", () => {
    expect(isTheme("ink")).toBe(true);
    expect(isTheme(42)).toBe(false);
  });
});

describe("themeFromCookieHeader", () => {
  it("reads the theme cookie among others", () => {
    expect(themeFromCookieHeader("session=abc; theme=ink; other=1")).toBe("ink");
  });

  it("ignores unrelated cookies and bad values", () => {
    expect(themeFromCookieHeader("session=abc")).toBe(DEFAULT_THEME);
    expect(themeFromCookieHeader("theme=neon")).toBe(DEFAULT_THEME);
    expect(themeFromCookieHeader("")).toBe(DEFAULT_THEME);
    expect(themeFromCookieHeader(null)).toBe(DEFAULT_THEME);
  });

  it("decodes a URL-encoded value", () => {
    expect(themeFromCookieHeader("theme=gre%79")).toBe("grey");
    expect(themeFromCookieHeader("theme=grey%20")).toBe(DEFAULT_THEME);
  });
});

describe("THEME_INIT_SCRIPT", () => {
  it("sets data-theme before first paint and embeds the cookie name", () => {
    expect(THEME_INIT_SCRIPT).toContain("documentElement");
    expect(THEME_INIT_SCRIPT).toContain("theme=");
    expect(THEME_INIT_SCRIPT).toContain('"paper"');
  });
});
