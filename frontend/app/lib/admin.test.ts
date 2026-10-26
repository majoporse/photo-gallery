import { describe, expect, it } from "vitest";
import type {
  CollectionAdminView,
  PhotoDetail,
} from "../api/generated/model";
import {
  buildPhotoEditPayload,
  byPosition,
  formatDateTimeLocal,
  hiddenCollections,
  isValidSlug,
  liveCollections,
  payloadFromPhotoDetail,
  slugFromName,
  textOrNull,
  toIsoDateTime,
  uploadRejectionReason,
} from "./admin";

function collection(
  id: string,
  position: number | string,
  deletedAt: string | null = null,
): CollectionAdminView {
  return {
    id,
    slug: id,
    name: id,
    description: null,
    position,
    photoCount: 0,
    deletedAt,
  };
}

describe("uploadRejectionReason", () => {
  it("turns every documented enum value into a readable sentence", () => {
    for (const code of [0, 1, 2, 3, 4]) {
      expect(uploadRejectionReason(code)).toMatch(/[a-z]/);
      expect(uploadRejectionReason(code)).not.toContain("reason code");
    }
  });

  it("keeps an unknown code visible instead of inventing a reason", () => {
    expect(uploadRejectionReason(9)).toContain("reason code 9");
  });

  it("also accepts a named rejection", () => {
    expect(uploadRejectionReason("TooSmall")).toBe(
      "Shorter than 640 px on the long edge.",
    );
  });
});

describe("slugs", () => {
  it("derives a slug from a name", () => {
    expect(slugFromName("Côte d'Azur — 2026")).toBe("cote-d-azur-2026");
    expect(slugFromName("  Streets / Night  ")).toBe("streets-night");
  });

  it("accepts only the API's slug shape", () => {
    expect(isValidSlug("streets-at-night")).toBe(true);
    expect(isValidSlug("a1")).toBe(true);
    expect(isValidSlug("Streets")).toBe(false);
    expect(isValidSlug("two--hyphens")).toBe(false);
    expect(isValidSlug("-leading")).toBe(false);
    expect(isValidSlug("trailing-")).toBe(false);
    expect(isValidSlug("")).toBe(false);
    expect(isValidSlug("a".repeat(121))).toBe(false);
  });
});

describe("photo form values", () => {
  it("round-trips a local datetime value", () => {
    const iso = "2026-05-04T10:30:00.000Z";
    const local = formatDateTimeLocal(iso);
    expect(toIsoDateTime(local)).toBe(iso);
  });

  it("treats an empty text field as null", () => {
    expect(textOrNull("   ")).toBeNull();
    expect(textOrNull(" caption ")).toBe("caption");
  });

  it("sends the whole form and clears unchecked featured rank", () => {
    const result = buildPhotoEditPayload({
      title: "  Night bus ",
      caption: "",
      altText: "A bus at night",
      takenAt: "2026-05-04T10:30",
      featured: false,
      featuredRank: "7",
      collectionId: "col-1",
    });

    expect(result).toEqual({
      ok: true,
      payload: {
        title: "Night bus",
        caption: null,
        altText: "A bus at night",
        takenAt: toIsoDateTime("2026-05-04T10:30"),
        featuredRank: null,
        collectionId: "col-1",
      },
    });
  });

  it("requires a whole rank when featured", () => {
    const result = buildPhotoEditPayload({
      title: "",
      caption: "",
      altText: "",
      takenAt: "",
      featured: true,
      featuredRank: "1.5",
      collectionId: "col-1",
    });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toContain("rank");
    }
  });

  it("rejects an invalid taken-at and a missing collection", () => {
    const base = {
      title: "",
      caption: "",
      altText: "",
      takenAt: "not a date",
      featured: false,
      featuredRank: "",
      collectionId: "col-1",
    };
    expect(buildPhotoEditPayload(base).ok).toBe(false);

    expect(
      buildPhotoEditPayload({
        ...base,
        takenAt: "",
        collectionId: "",
      }).ok,
    ).toBe(false);
  });

  it("rebuilds a full PATCH from public photo detail", () => {
    const detail: PhotoDetail = {
      photo: {
        id: "p1",
        sha256: "abc",
        width: 800,
        height: 600,
        availableWidths: [320, 640],
        dominantColor: "#000000",
        title: "Title",
        altText: "Alt",
        featuredRank: null,
        takenAt: "2026-01-02T03:04:05.000Z",
      },
      caption: "Caption",
      collectionSlug: "streets",
      collectionName: "Streets",
      exif: {},
      previousPhotoId: null,
      nextPhotoId: null,
    };

    expect(payloadFromPhotoDetail(detail, "col-1", 3)).toEqual({
      title: "Title",
      caption: "Caption",
      altText: "Alt",
      takenAt: "2026-01-02T03:04:05.000Z",
      featuredRank: 3,
      collectionId: "col-1",
    });
  });
});

describe("collection order", () => {
  const collections = [
    collection("b", 2),
    collection("hidden", 1, "2026-01-01T00:00:00Z"),
    collection("a", 0),
  ];

  it("sorts live collections by position", () => {
    expect(liveCollections(collections).map((item) => item.id)).toEqual([
      "a",
      "b",
    ]);
  });

  it("keeps hidden collections out of the sortable list", () => {
    expect(hiddenCollections(collections).map((item) => item.id)).toEqual([
      "hidden",
    ]);
  });

  it("compares numeric positions that arrive as strings", () => {
    expect(byPosition(collection("x", "10"), collection("y", 2))).toBe(8);
  });
});
