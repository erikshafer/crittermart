import { Link } from "@tanstack/react-router";

// The router's not-found component (registered on both the root route and the router, src/router.tsx).
export function RouteNotFound() {
  return (
    <div className="space-y-3">
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="text-muted-foreground">
        That page doesn&apos;t exist.{" "}
        <Link to="/" className="underline underline-offset-4">
          Back to the storefront
        </Link>
        .
      </p>
    </div>
  );
}
