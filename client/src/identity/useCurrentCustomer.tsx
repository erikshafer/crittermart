import { createContext, useContext, type ReactNode } from "react";

// The round-one identity seam (ADR 009). Identity is stubbed: a single hardcoded customer id, exposed
// through one React hook so that **no component ever reads the stubbed value directly** — they call
// useCurrentCustomer(). There is no login screen.
//
// This hook is the ONE place the Polecat promotion touches: today it returns the stubbed id; later it
// reads the authenticated claim. Because the shared HTTP client (src/api/client.ts) sets the id once as
// the X-Customer-Id header from this seam, the promotion is a localized header->Bearer-claim swap with
// every call site unchanged — not a sweep that edits every request.
//
// The id is overridable via the provider prop purely to make the seam testable and to leave room for a
// future dev-only customer switcher; production round one always uses the stub.
const STUBBED_CUSTOMER_ID = "customer-demo";

const CurrentCustomerContext = createContext<string>(STUBBED_CUSTOMER_ID);

export function CurrentCustomerProvider({
  customerId = STUBBED_CUSTOMER_ID,
  children,
}: {
  customerId?: string;
  children: ReactNode;
}) {
  return (
    <CurrentCustomerContext.Provider value={customerId}>{children}</CurrentCustomerContext.Provider>
  );
}

// Returns the current customer's id. Customer-keyed reads/commands carry it ambiently via the
// X-Customer-Id header set on the shared client; callers rarely need the raw value, but components that
// must branch on identity read it here rather than the stub.
export function useCurrentCustomer(): string {
  return useContext(CurrentCustomerContext);
}
