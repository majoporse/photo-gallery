import { describe, expect, it } from "vitest";
import {
  FIXTURE_PHOTO_ID,
  fixtureCollection,
  fixtureCollectionPhotos,
  fixtureCollections,
  fixtureHome,
  fixturePhotoDetail,
} from "./fixtures";

const CANONICAL_WIDTHS = new Set([320, 640, 1280, 2048]);

describe("fixtures", () => {
  it("has a coherent home feed", () => {
    expect(fixtureHome.featured.length).toBeGreaterThan(0);
    expect(fixtureHome.collections.length).toBe(3);
    expect(fixtureCollections.total).toBe(3);
    expect(fixtureCollections.items.map((item) => item.slug)).toEqual(
      fixtureHome.collections.map((item) => item.slug),
    );
  });

  it("gives every photo the public summary fields the contract requires", () => {
    const photos = fixtureHome.featured;

    for (const photo of photos) {
      expect(photo.id).toMatch(/^[0-9A-HJKMNP-TV-Z]{26}$/);
      expect(photo.sha256).toMatch(/^[0-9a-f]{64}$/);
      expect(Number(photo.width)).toBeGreaterThan(0);
      expect(Number(photo.height)).toBeGreaterThan(0);
      expect(photo.dominantColor).toMatch(/^#[0-9a-f]{6}$/i);
      expect(photo.availableWidths.length).toBeGreaterThan(0);
      expect(
        photo.availableWidths.every((width) => CANONICAL_WIDTHS.has(Number(width))),
      ).toBe(true);
    }
  });

  it("keeps photo ids unique across collections", () => {
    const ids = fixtureCollections.items.flatMap((collection) => {
      const photos = fixtureCollectionPhotos(collection.slug, 1);
      expect(photos).toBeDefined();
      return photos?.items.map((item) => item.id) ?? [];
    });

    expect(new Set(ids).size).toBe(ids.length);
    expect(ids).toContain(FIXTURE_PHOTO_ID);
  });

  it("404s unknown collections and photos", () => {
    expect(fixtureCollection("no-such-collection")).toBeUndefined();
    expect(fixtureCollectionPhotos("no-such-collection", 1)).toBeUndefined();
    expect(fixturePhotoDetail("no-such-photo")).toBeUndefined();
  });

  it("links previous and next within a collection", () => {
    const detail = fixturePhotoDetail(FIXTURE_PHOTO_ID);
    expect(detail).toBeDefined();
    expect(detail?.collectionSlug).toBe("quiet-coast");
    expect(detail?.previousPhotoId).toBeNull();
    expect(detail?.nextPhotoId).toBe("01JQZ5F8S4M2X9K7C3V1B6N0A2");
    expect(detail?.exif).toMatchObject({ camera: "Fixture Camera" });
  });

  it("paginates deterministically", () => {
    const first = fixtureCollectionPhotos("quiet-coast", 1);
    const second = fixtureCollectionPhotos("quiet-coast", 2);

    expect(first?.page).toBe(1);
    expect(first?.pageSize).toBe(24);
    expect(first?.items.length).toBe(4);
    expect(second?.items).toEqual([]);
  });
});
