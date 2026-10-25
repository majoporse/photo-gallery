import { describe, expect, it } from "vitest";
import {
  buildSrcSet,
  derivativeUrl,
  largestWidth,
  normalizeWidths,
  photoAltText,
  pickWidth,
  preferredWidth,
  scaledHeight,
} from "./images";

const BASE = "https://img.example.test";
const SHA = "a".repeat(64);

describe("derivativeUrl", () => {
  it("builds the content-addressed derivative URL", () => {
    expect(derivativeUrl(BASE, SHA, 1280, "avif")).toBe(
      `https://img.example.test/photos/derivatives/${SHA}/1280w.avif`,
    );
  });

  it("defaults to JPEG and tolerates a trailing slash", () => {
    expect(derivativeUrl(`${BASE}/`, SHA, 640)).toBe(
      `https://img.example.test/photos/derivatives/${SHA}/640w.jpg`,
    );
  });

  it("does not hardcode a host", () => {
    expect(derivativeUrl("http://localhost:8333", SHA, 320, "webp")).toBe(
      `http://localhost:8333/photos/derivatives/${SHA}/320w.webp`,
    );
  });
});

describe("normalizeWidths", () => {
  it("coerces strings, removes duplicates and sorts ascending", () => {
    expect(normalizeWidths([1280, "320", 640, "640", 1280])).toEqual([
      320, 640, 1280,
    ]);
  });

  it("drops non-positive and non-integer values", () => {
    expect(normalizeWidths([0, -320, 320.5, "abc", 640])).toEqual([640]);
  });
});

describe("pickWidth", () => {
  it("chooses the largest width that does not upscale", () => {
    expect(pickWidth([320, 640, 1280, 2048], 1280)).toBe(1280);
    expect(pickWidth([320, 640], 1280)).toBe(640);
    expect(pickWidth(["320", "640"], 640)).toBe(640);
  });

  it("falls back to the smallest width when everything is larger", () => {
    expect(pickWidth([640, 1280], 320)).toBe(640);
  });

  it("returns undefined for an empty ladder", () => {
    expect(pickWidth([], 1280)).toBeUndefined();
  });
});

describe("buildSrcSet", () => {
  it("emits one candidate per width in ascending order", () => {
    expect(buildSrcSet(BASE, SHA, [1280, "320", 640, "320"], "webp")).toBe(
      [
        `https://img.example.test/photos/derivatives/${SHA}/320w.webp 320w`,
        `https://img.example.test/photos/derivatives/${SHA}/640w.webp 640w`,
        `https://img.example.test/photos/derivatives/${SHA}/1280w.webp 1280w`,
      ].join(", "),
    );
  });

  it("handles a ladder that stops below 1280", () => {
    expect(buildSrcSet(BASE, SHA, [320, 640], "avif")).toBe(
      [
        `https://img.example.test/photos/derivatives/${SHA}/320w.avif 320w`,
        `https://img.example.test/photos/derivatives/${SHA}/640w.avif 640w`,
      ].join(", "),
    );
  });
});

describe("preferredWidth", () => {
  it("prefers 1280 and never upscales above the ladder", () => {
    expect(preferredWidth([320, 640, 1280, 2048])).toBe(1280);
    expect(preferredWidth([320, 640])).toBe(640);
    expect(preferredWidth([320, 640, 2048])).toBe(640);
  });

  it("falls back to the largest available derivative", () => {
    expect(preferredWidth([2048])).toBe(2048);
    expect(preferredWidth([2048, 4096])).toBe(2048);
  });
});

describe("largestWidth", () => {
  it("returns the largest normalised width", () => {
    expect(largestWidth([640, "320", 1280])).toBe(1280);
    expect(largestWidth([])).toBeUndefined();
  });
});

describe("scaledHeight", () => {
  it("preserves the aspect ratio", () => {
    expect(scaledHeight(1600, 1000, 640)).toBe(400);
    expect(scaledHeight(4000, 6000, 320)).toBe(480);
  });

  it("returns zero for an invalid source", () => {
    expect(scaledHeight(0, 100, 320)).toBe(0);
    expect(scaledHeight("nope", 100, 320)).toBe(0);
  });
});

describe("photoAltText", () => {
  it("prefers alt text, then title, then a description", () => {
    expect(photoAltText({ altText: "A grey sea", title: "Tide" })).toBe(
      "A grey sea",
    );
    expect(photoAltText({ altText: null, title: "Tide" })).toBe("Tide");
    expect(photoAltText({ altText: "  ", title: null })).toBe(
      "Untitled photograph",
    );
  });
});
