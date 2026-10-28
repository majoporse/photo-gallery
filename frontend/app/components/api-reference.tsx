import { ApiReferenceReact } from "@scalar/api-reference-react";
import "@scalar/api-reference-react/style.css";

/**
 * Loaded only from `/api-docs`, through a dynamic import: the reference is a
 * developer surface and its bundle must never reach the gallery routes.
 */
export default function ApiReference() {
  return (
    <ApiReferenceReact
      configuration={{
        url: "/openapi.json",
        withDefaultFonts: false,
      }}
    />
  );
}
