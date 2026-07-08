import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";

import "./index.css";
import { router } from "@/router";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

// One QueryClient owns server state for the whole app (ADR 015). A short staleTime keeps cart/order
// reads fresh between interactions; because there is no real-time push round one (ADR 015 R5), order
// status converges by refetch (on-focus / invalidate), not a socket.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
    },
  },
});

// Provider order: QueryClientProvider (the cache mutations write to) -> CurrentCustomerProvider (the auth
// seam — seeds the session from the persisted JWT, ADR 023; the shared client reads its token for the
// Authorization: Bearer header) -> RouterProvider (pages).
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <CurrentCustomerProvider>
        <RouterProvider router={router} />
      </CurrentCustomerProvider>
    </QueryClientProvider>
  </StrictMode>,
);
