import { reactRouter } from "@react-router/dev/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  // Vite only exposes `VITE_*` to the browser bundle. The SSR side reads
  // process.env, so copy every key from .env into it — without overwriting
  // values the container or shell already provided.
  const fileEnv = loadEnv(mode, process.cwd(), "");

  for (const [key, value] of Object.entries(fileEnv)) {
    if (process.env[key] === undefined) {
      process.env[key] = value;
    }
  }

  return {
    plugins: [tailwindcss(), reactRouter()],
    resolve: {
      tsconfigPaths: true,
    },
  };
});
