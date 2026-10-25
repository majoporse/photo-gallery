const LOCAL_API_URL = "http://localhost:8080";
const LOCAL_IMAGE_URL = "http://localhost:8333";
const LOCAL_SITE_URL = "http://localhost:3000";

export function fixturesEnabled(): boolean {
  return process.env.FIXTURES === "1";
}

export function getApiInternalUrl(): string {
  return (process.env.API_INTERNAL_URL ?? LOCAL_API_URL).replace(/\/$/, "");
}

/**
 * The chart provides PUBLIC_SITE_URL but not PUBLIC_API_URL. When the latter is
 * absent, derive the api. host that the spec reserves next to the site host.
 */
export function getPublicApiUrl(): string {
  const explicit = process.env.PUBLIC_API_URL?.trim();
  if (explicit) {
    return explicit.replace(/\/$/, "");
  }

  const site = process.env.PUBLIC_SITE_URL;
  if (site) {
    try {
      const url = new URL(site);
      if (url.hostname !== "localhost" && url.hostname !== "127.0.0.1") {
        url.hostname = `api.${url.hostname}`;
        url.pathname = "";
        url.search = "";
        url.hash = "";
        return url.origin;
      }
    } catch {
      // Fall through to the local default.
    }
  }

  return LOCAL_API_URL;
}

export function getImageBaseUrl(): string {
  return (process.env.IMAGE_BASE_URL ?? LOCAL_IMAGE_URL).replace(/\/$/, "");
}

export function getSiteUrl(): string {
  return (process.env.PUBLIC_SITE_URL ?? LOCAL_SITE_URL).replace(/\/$/, "");
}
