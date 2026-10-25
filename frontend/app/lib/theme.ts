import { parseCookie } from "cookie";

export const THEMES = ["paper", "ink", "grey"] as const;
export type Theme = (typeof THEMES)[number];

export const DEFAULT_THEME: Theme = "paper";
export const THEME_COOKIE = "theme";
export const THEME_COOKIE_MAX_AGE = 60 * 60 * 24 * 365;

declare global {
  interface Window {
    __PUBLIC_API_URL__?: string;
  }
}

export function isTheme(value: unknown): value is Theme {
  return (
    typeof value === "string" && (THEMES as readonly string[]).includes(value)
  );
}

export function parseTheme(value: string | null | undefined): Theme {
  return isTheme(value) ? value : DEFAULT_THEME;
}

export function themeFromCookieHeader(
  header: string | null | undefined,
): Theme {
  if (!header) {
    return DEFAULT_THEME;
  }

  return parseTheme(parseCookie(header)[THEME_COOKIE]);
}

/**
 * Runs in <head> before the stylesheet. The server also sets data-theme from
 * the cookie, so this mainly makes the choice survive a cached HTML response.
 */
export const THEME_INIT_SCRIPT = `(function(){try{var m=document.cookie.match(/(?:^|; )${THEME_COOKIE}=([^;]*)/);var v=m?decodeURIComponent(m[1]):"";if(v==="paper"||v==="ink"||v==="grey"){document.documentElement.setAttribute("data-theme",v)}}catch(e){}})();`;

export function writeThemeCookie(theme: Theme): void {
  const secure =
    typeof window !== "undefined" && window.location.protocol === "https:"
      ? "; Secure"
      : "";
  document.cookie = `${THEME_COOKIE}=${theme}; Path=/; Max-Age=${THEME_COOKIE_MAX_AGE}; SameSite=Lax${secure}`;
}
