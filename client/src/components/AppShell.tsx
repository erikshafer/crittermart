import { Link, Outlet, useNavigate } from "@tanstack/react-router";

import { CartBadge } from "@/cart/CartBadge";
import { useAuth } from "@/identity/useCurrentCustomer";

// The storefront's outer chrome — header + routed content. A pure layout component: it fetches nothing
// itself (the cart badge owns its own query subscription, so only the badge re-renders on a count change).
// The header links are the type-safe TanStack Router `Link` (code-based router, src/router.tsx).
export function AppShell() {
  const { isAuthenticated, customerId, logout } = useAuth();
  const navigate = useNavigate();

  function onLogout() {
    logout(); // client-side token discard (slice 5.11)
    void navigate({ to: "/" });
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <Link to="/" className="text-xl font-semibold tracking-tight">
            CritterMart
          </Link>
          <nav className="flex items-center gap-6">
            {isAuthenticated ? (
              <>
                <Link
                  to="/orders"
                  className="text-sm font-medium text-muted-foreground hover:text-foreground"
                >
                  My Orders
                </Link>
                <CartBadge />
                <span className="text-sm text-muted-foreground" title={customerId ?? undefined}>
                  {customerId}
                </span>
                <button
                  type="button"
                  onClick={onLogout}
                  className="text-sm font-medium text-muted-foreground hover:text-foreground"
                >
                  Log out
                </button>
              </>
            ) : (
              <>
                <Link
                  to="/login"
                  className="text-sm font-medium text-muted-foreground hover:text-foreground"
                >
                  Log in
                </Link>
                <Link
                  to="/register"
                  className="rounded-md bg-foreground px-3 py-1.5 text-sm font-medium text-background"
                >
                  Register
                </Link>
              </>
            )}
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        <Outlet />
      </main>
    </div>
  );
}
