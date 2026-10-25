import { describe, expect, it } from "vitest";
import {
  buildSrcSet,
  derivativeUrl,
  normalizeWidths,
  pickWidth,
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
});
