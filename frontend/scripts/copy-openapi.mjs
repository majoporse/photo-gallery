import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// The committed contract is the single source of truth: CI drift-checks it and
// Orval generates the client from it. `/api-docs` fetches this copy, so the
// reference page can never describe a different API than the one that ships.
const frontend = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const source = resolve(frontend, "..", "contracts", "openapi.json");
const target = resolve(frontend, "public", "openapi.json");

mkdirSync(dirname(target), { recursive: true });
copyFileSync(source, target);
