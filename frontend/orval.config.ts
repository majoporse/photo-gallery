import { defineConfig } from "orval";

export default defineConfig({
  gallery: {
    input: "../contracts/openapi.json",
    output: {
      target: "./app/api/generated/gallery.ts",
      schemas: "./app/api/generated/model",
      client: "react-query",
      httpClient: "fetch",
      mode: "single",
      clean: true,
      override: {
        mutator: {
          path: "./app/api/http.ts",
          name: "apiFetch",
        },
        fetch: {
          includeHttpResponseReturnType: false,
          forceSuccessResponse: true,
        },
        query: {
          useQuery: true,
          signal: true,
        },
      },
    },
  },
});
