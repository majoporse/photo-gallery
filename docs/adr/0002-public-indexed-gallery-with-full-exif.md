# The gallery is public, indexed, and publishes full EXIF including GPS

The site exists to share photographs with friends and family, but it is deliberately **public and search-engine indexable**, and it exposes the **complete EXIF block, including GPS coordinates and camera serial numbers**. The alternative — stripping GPS, or gating the site behind a password — was considered twice and rejected.

**Consequences.** Any photograph uploaded becomes world-readable at a stable URL and may be crawled, cached, scraped and archived indefinitely; deleting it later removes it from this site but not from the internet. Photographs taken at home disclose the location of that home, and camera serial numbers link photographs across sites. Uploading is therefore a publishing act with no undo, and the Admin UI should say so once rather than pretend otherwise.

Do not "helpfully" strip GPS later without reopening this decision: the absence of that filter is deliberate, not an oversight.
