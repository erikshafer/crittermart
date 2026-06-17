import { Link, Outlet } from "@tanstack/react-router";

import { CartBadge } from "@/cart/CartBadge";

// The storefront's outer chrome — header + routed content. A pure layout component: it fetches nothing
// itself (the cart badge owns its own query subscription, so only the badge re-renders on a count change).
// The header links are the type-safe TanStack Router `Link` (code-based router, src/router.tsx).
export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <Link to="/" className="text-xl font-semibold tracking-tight">
            CritterMart
          </Link>
          <nav className="flex items-center gap-6">
            <Link
              to="/orders"
              className="text-sm font-medium text-muted-foreground hover:text-foreground"
            >
              My Orders
            </Link>
            <CartBadge />
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        <Outlet />
      </main>
    </div>
  );
}
