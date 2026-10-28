import {
  type RouteConfig,
  index,
  route,
} from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("collections", "routes/collections.tsx"),
  route("collections/:slug", "routes/collection.tsx"),
  route("photos/:id", "routes/photo.tsx"),
  route("about", "routes/about.tsx"),
  route("api-docs", "routes/api-docs.tsx"),
  route("admin", "routes/admin.tsx", [
    index("routes/admin-login.tsx"),
    route("collections", "routes/admin-collections.tsx"),
    route("collections/:slug", "routes/admin-collection.tsx"),
    route("collections/:slug/edit", "routes/admin-collection-settings.tsx"),
    route("photos/:id", "routes/admin-photo.tsx"),
  ]),
  route("healthz", "routes/healthz.tsx"),
  route("sitemap.xml", "routes/sitemap[.]xml.ts"),
  route("robots.txt", "routes/robots[.]txt.ts"),
] satisfies RouteConfig;
