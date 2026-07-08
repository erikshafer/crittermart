import { Navigate } from "@tanstack/react-router";
import type { ReactNode } from "react";

import { useAuth } from "@/identity/useCurrentCustomer";

// Route gate for the customer-keyed screens (ADR 023: browse anonymously, checkout requires login). An
// unauthenticated visitor to a gated route is redirected to /login; an authenticated one sees the content.
// The resource server is the real enforcement (a request with no valid token is 401'd server-side, slice
// 5.10) — this is the UX layer that sends the customer to log in rather than showing them a broken screen.
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/login" />;
  }
  return <>{children}</>;
}
