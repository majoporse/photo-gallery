import { reactRouter } from "@react-router/dev/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ command, mode }) => {
  // Vite only exposes `VITE_*` to the browser bundle. The SSR side reads
  // process.env, so copy every key from .env into it — without overwriting
  // values the container or shell already provided.
  const fileEnv = loadEnv(mode, process.cwd(), "");

  for (const [key, value] of Object.entries(fileEnv)) {
    if (process.env[key] === undefined) {
      process.env[key] = value;
    }
  }

  if (command === "serve") {
    // A stale export silently beats .env, and the symptom is an inscrutable
    // "fetch failed" in the loader. Say which address the server will use.
    const apiUrl = process.env.API_INTERNAL_URL ?? "http://localhost:8080";
    const from = process.env.API_INTERNAL_URL ? "shell" : "default";
    console.log(`[env] SSR API base: ${apiUrl} (${from})`);
  }

  // The browser talks to the API directly for admin calls, so this server's
  // origin must match the API's CORS allow-list — which Compose derives from
  // PUBLIC_SITE_URL. Follow that URL's port instead of Vite's default 5173.
  const siteUrl = process.env.PUBLIC_SITE_URL ?? "http://localhost:3000";
  const port = Number(new URL(siteUrl).port || 3000);

  return {
    plugins: [tailwindcss(), reactRouter()],
    resolve: {
      tsconfigPaths: true,
    },
    server: {
      // Drifting to another port breaks the browser-only admin calls, so fail
      // loudly rather than silently moving to the next free one.
      port,
      strictPort: true,
    },
  };
});
