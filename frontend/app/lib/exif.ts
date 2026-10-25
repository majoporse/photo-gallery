const ACRONYM = /^[A-Z]{2,}$/;

function isAcronym(word: string): boolean {
  return ACRONYM.test(word);
}

/**
 * Magick.NET serialises tag names as PascalCase ("GPSLatitude",
 * "DateTimeOriginal"). Split them into words for display; the API contract is
 * untouched, so nothing is filtered or renamed.
 */
export function formatExifLabel(key: string): string {
  const spaced = key
    .replace(/([a-z\d])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim();

  if (!spaced) {
    return key;
  }

  const words = spaced.split(/\s+/);
  return words
    .map((word, index) => {
      if (isAcronym(word)) {
        return word;
      }

      const lower = word.toLowerCase();
      return index === 0 ? lower.charAt(0).toUpperCase() + lower.slice(1) : lower;
    })
    .join(" ");
}

export function exifEntries(
  exif: Record<string, string>,
): Array<[key: string, label: string, value: string]> {
  return Object.keys(exif)
    .sort((a, b) => a.localeCompare(b))
    .map((key) => [key, formatExifLabel(key), exif[key] ?? ""]);
}
