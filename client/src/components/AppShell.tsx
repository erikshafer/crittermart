import { Link, Outlet } from "@tanstack/react-router";

// The storefront's outer chrome — header + routed content. Deliberately thin at bootstrap: the cart
// badge, nav, and footer arrive with the screen slices (W1–W4). The header link is the type-safe
// TanStack Router `Link` (code-based router, src/router.tsx).
export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <Link to="/" className="text-xl font-semibold tracking-tight">
            CritterMart
          </Link>
          <span className="text-sm text-muted-foreground">storefront</span>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        <Outlet />
      </main>
    </div>
  );
}
