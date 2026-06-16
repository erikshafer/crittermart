import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";

import { AppShell } from "@/components/AppShell";
import { RouteNotFound } from "@/components/RouteNotFound";
import { BrowsePage } from "@/catalog/BrowsePage";
import { CartPage } from "@/cart/CartPage";

// Code-based route tree (ADR 015 amendment — TanStack Router, wired code-based, no route-tree codegen).
// Chosen for shared lineage with the already-accepted TanStack Query and for type-safe routes +
// search-params-as-state at the storefront's small route count. With a handful of routes, code-based
// composition needs no router plugin and emits no generated file, keeping each slice reviewable; a
// later slice can migrate to file-based routing if the route count grows. The screen slices add their
// routes here (W1 `/`, W2 `/cart`, W3 `/orders/$id/confirmation`, W4 `/orders/$id`).
const rootRoute = createRootRoute({
  component: AppShell,
  notFoundComponent: RouteNotFound,
});

// W1 — Browse / Listing. The storefront landing: the product grid + add-to-cart (Narrative 005 Moments 1–2).
// This takes `/` — the customer lands ON the listing (the bootstrap HomePage wiring-check placeholder was
// retired once a real landing screen existed; prompt 019 locked decision 2).
const browseRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: BrowsePage,
});

// W2 — Cart Review. Renders the customer's open cart from GET /carts/mine.
const cartRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/cart",
  component: CartPage,
});

const routeTree = rootRoute.addChildren([browseRoute, cartRoute]);

export const router = createRouter({
  routeTree,
  defaultNotFoundComponent: RouteNotFound,
});

// Register the router instance for project-wide type inference (typed Link `to`, params, search).
declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
