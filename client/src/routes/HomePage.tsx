import { serviceUrls } from "@/config";
import { useCurrentCustomer } from "@/identity/useCurrentCustomer";

// Bootstrap landing page. NOT a modeled screen — W1 Browse / W2 Cart Review / W3 Order Confirmation /
// W4 Order Status are the screen slices that follow (workshop § 5.1). This page exists to prove the
// scaffold is wired end-to-end: React + code-based router render, Tailwind v4 styles apply, the identity
// seam resolves, and the three service base URLs arrive from Aspire (or the dev fallbacks). The wiring
// panel is a developer aid for the demo — it makes the cross-origin topology visible — not storefront UI.
export function HomePage() {
  const customerId = useCurrentCustomer();

  const services = [
    { name: "Catalog", url: serviceUrls.catalogUrl },
    { name: "Inventory", url: serviceUrls.inventoryUrl },
    { name: "Orders", url: serviceUrls.ordersUrl },
  ];

  return (
    <div className="space-y-8">
      <section className="space-y-2">
        <h1 className="text-3xl font-semibold tracking-tight">Welcome to CritterMart</h1>
        <p className="text-muted-foreground">
          The storefront scaffold is up. Shopping screens land in the screen slices that follow this
          bootstrap.
        </p>
      </section>

      <section className="rounded-lg border border-border p-5">
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Wiring check</h2>
        <dl className="space-y-2 text-sm">
          <div className="flex gap-3">
            <dt className="w-24 shrink-0 text-muted-foreground">Customer</dt>
            <dd className="font-mono">{customerId}</dd>
          </div>
          {services.map((service) => (
            <div key={service.name} className="flex gap-3">
              <dt className="w-24 shrink-0 text-muted-foreground">{service.name}</dt>
              <dd className="font-mono">{service.url}</dd>
            </div>
          ))}
        </dl>
      </section>
    </div>
  );
}
