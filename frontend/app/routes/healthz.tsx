/**
 * Resource route: no component, so React Router serves the loader's Response
 * directly. It must never touch the API or the database.
 */
export function loader() {
  return new Response("ok", {
    status: 200,
    headers: {
      "Content-Type": "text/plain; charset=utf-8",
      "Cache-Control": "no-store",
    },
  });
}
