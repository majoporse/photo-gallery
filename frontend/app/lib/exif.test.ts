import { describe, expect, it } from "vitest";
import { exifEntries, formatExifLabel } from "./exif";

describe("formatExifLabel", () => {
  it("splits PascalCase and keeps acronyms together", () => {
    expect(formatExifLabel("GPSLatitude")).toBe("GPS latitude");
    expect(formatExifLabel("ISOSpeedRatings")).toBe("ISO speed ratings");
    expect(formatExifLabel("FNumber")).toBe("F number");
  });

  it("keeps single words and tolerates separators", () => {
    expect(formatExifLabel("Make")).toBe("Make");
    expect(formatExifLabel("date_time_original")).toBe("Date time original");
  });
});

describe("exifEntries", () => {
  it("keeps every key, sorts them and gives each a readable label", () => {
    const entries = exifEntries({
      Model: "X-T5",
      Make: "Fujifilm",
      GPSLatitude: "51/1,30/1,0/1",
    });

    expect(entries).toEqual([
      ["GPSLatitude", "GPS latitude", "51/1,30/1,0/1"],
      ["Make", "Make", "Fujifilm"],
      ["Model", "Model", "X-T5"],
    ]);
  });
});
