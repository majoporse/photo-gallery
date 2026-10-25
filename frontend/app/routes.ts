import { type RouteConfig, index, route } from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("collections", "routes/collections.tsx"),
  route("collections/:slug", "routes/collection.tsx"),
  route("photos/:id", "routes/photo.tsx"),
  route("about", "routes/about.tsx"),
  route("admin", "routes/admin.tsx"),
  route("healthz", "routes/healthz.tsx"),
  route("sitemap.xml", "routes/sitemap[.]xml.ts"),
  route("robots.txt", "routes/robots[.]txt.ts"),
] satisfies RouteConfig;
