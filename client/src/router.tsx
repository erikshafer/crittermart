import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";

import { AppShell } from "@/components/AppShell";
import { RequireAuth } from "@/components/RequireAuth";
import { RouteNotFound } from "@/components/RouteNotFound";
import { BrowsePage } from "@/catalog/BrowsePage";
import { CartPage } from "@/cart/CartPage";
import { LoginPage } from "@/identity/LoginPage";
import { RegisterPage } from "@/identity/RegisterPage";
import { MyOrdersPage } from "@/orders/MyOrdersPage";
import { OrderConfirmationPage } from "@/orders/OrderConfirmationPage";
import { OrderStatusPage } from "@/orders/OrderStatusPage";

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

// Auth screens (ADR 023, Narrative 010). Public routes — the login/register forms themselves need no session.
const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/login",
  component: LoginPage,
});

const registerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/register",
  component: RegisterPage,
});

// W2 — Cart Review. Renders the customer's open cart from GET /carts/mine. GATED (ADR 023): the cart is
// customer-keyed, so an unauthenticated visitor is redirected to /login by RequireAuth.
const cartRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/cart",
  component: () => (
    <RequireAuth>
      <CartPage />
    </RequireAuth>
  ),
});

// "My Orders" list (Narrative 005 Moment 6; workshop § 5.1 Gap #3). Reads GET /orders/mine (customer-keyed)
// and lists the customer's orders newest-first, each row linking into the W4 track screen. A literal /orders
// segment, distinct from the /orders/$orderId param routes below. GATED (ADR 023).
const myOrdersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/orders",
  component: () => (
    <RequireAuth>
      <MyOrdersPage />
    </RequireAuth>
  ),
});

// W3 — Order Confirmation (Narrative 005 Moment 4). Reached by navigate() after [ Place Order ]'s POST /orders
// succeeds. This thin route component reads the {orderId} path param and hands it to the (router-free, pure)
// OrderConfirmationPage, which reads GET /orders/{orderId}. Referencing the route inside its own component is
// the canonical code-based pattern — the closure resolves at render time, after the const is assigned.
const orderConfirmationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/orders/$orderId/confirmation",
  component: function OrderConfirmationRoute() {
    const { orderId } = orderConfirmationRoute.useParams();
    return (
      <RequireAuth>
        <OrderConfirmationPage orderId={orderId} />
      </RequireAuth>
    );
  },
});

// W4 — Order Status / Tracking (Narrative 005 Moment 5). Reached by W3's now-live [ Track this order ] link.
// The thin route component reads the {orderId} path param and hands it to the (router-free, pure)
// OrderStatusPage, which reads GET /orders/{orderId} and polls it to convergence (refetchInterval, ADR 015
// R5 — no socket). Sibling of the confirmation route under the same /orders/$orderId prefix; the matcher
// resolves the more specific /confirmation path to W3, the bare param to W4.
const orderTrackingRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/orders/$orderId",
  component: function OrderTrackingRoute() {
    const { orderId } = orderTrackingRoute.useParams();
    return (
      <RequireAuth>
        <OrderStatusPage orderId={orderId} />
      </RequireAuth>
    );
  },
});

const routeTree = rootRoute.addChildren([
  browseRoute,
  loginRoute,
  registerRoute,
  cartRoute,
  myOrdersRoute,
  orderConfirmationRoute,
  orderTrackingRoute,
]);

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
