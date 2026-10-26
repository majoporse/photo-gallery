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

  return {
    plugins: [tailwindcss(), reactRouter()],
    resolve: {
      tsconfigPaths: true,
    },
  };
});
