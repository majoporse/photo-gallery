import { afterEach, describe, expect, it, vi } from "vitest";
import { getEventsUrl } from "./env.server";

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("getEventsUrl", () => {
  it("puts the stream on the notifier's port beside a local API", () => {
    vi.stubEnv("PUBLIC_API_URL", "");
    vi.stubEnv("PUBLIC_EVENTS_URL", "");
    vi.stubEnv("PUBLIC_SITE_URL", "http://localhost:3000");

    expect(getEventsUrl()).toBe("http://localhost:8081");
  });

  it("keeps the API host in a deployment, where the ingress routes the stream", () => {
    vi.stubEnv("PUBLIC_API_URL", "");
    vi.stubEnv("PUBLIC_EVENTS_URL", "");
    vi.stubEnv("PUBLIC_SITE_URL", "https://gallery.example");

    expect(getEventsUrl()).toBe("https://api.gallery.example");
  });

  it("prefers an explicit PUBLIC_EVENTS_URL and drops a trailing slash", () => {
    vi.stubEnv("PUBLIC_API_URL", "https://api.gallery.example");
    vi.stubEnv("PUBLIC_EVENTS_URL", "https://events.gallery.example/");

    expect(getEventsUrl()).toBe("https://events.gallery.example");
  });
});
