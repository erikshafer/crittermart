import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";

import { HomePage } from "@/routes/HomePage";
import { CurrentCustomerProvider } from "@/identity/useCurrentCustomer";

// Render smoke test — proves the React + jsdom + Testing Library harness works and that the page reads
// the identity seam and the parsed service config without throwing.
describe("HomePage", () => {
  it("renders the storefront heading and the wiring-check services", () => {
    render(
      <CurrentCustomerProvider customerId="customer-test">
        <HomePage />
      </CurrentCustomerProvider>,
    );

    expect(screen.getByRole("heading", { name: /welcome to crittermart/i })).toBeInTheDocument();
    expect(screen.getByText("customer-test")).toBeInTheDocument();
    expect(screen.getByText("Catalog")).toBeInTheDocument();
    expect(screen.getByText("Inventory")).toBeInTheDocument();
    expect(screen.getByText("Orders")).toBeInTheDocument();
  });
});
