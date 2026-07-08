import { z } from "zod";

// The storefront's three service base URLs (ADR 006 — no BFF; ADR 018 — direct cross-origin calls).
//
// Source of truth at runtime: Aspire's AddViteApp injects VITE_CATALOG_URL / VITE_INVENTORY_URL /
// VITE_ORDERS_URL into this app's process env (see src/CritterMart.AppHost/Program.cs). Vite exposes
// VITE_-prefixed vars on import.meta.env. When the SPA runs standalone (`npm run dev` without the
// AppHost) the vars are absent, so each falls back to the service's launchSettings port — the same
// ports the dev `dotnet run` of each service listens on (Catalog 5101, Inventory 5102, Orders 5103).
//
// The URLs are parsed through Zod at this boundary, exactly as every wire response is (Convention 2):
// a malformed or missing-and-unfallback-able URL fails loudly here at startup, not three components
// deep at the first fetch.
const DEV_FALLBACKS = {
  catalog: "http://localhost:5101",
  inventory: "http://localhost:5102",
  orders: "http://localhost:5103",
  // Identity is browser-facing as of the auth slices (register/login/logout — ADR 023). Its dev HTTP
  // port is 5105 (Properties/launchSettings.json), injected in the AppHost as VITE_IDENTITY_URL.
  identity: "http://localhost:5105",
} as const;

const serviceUrlsSchema = z.object({
  catalogUrl: z.url(),
  inventoryUrl: z.url(),
  ordersUrl: z.url(),
  identityUrl: z.url(),
});

export type ServiceUrls = z.infer<typeof serviceUrlsSchema>;

function stripTrailingSlash(url: string): string {
  return url.endsWith("/") ? url.slice(0, -1) : url;
}

// Parsed once at module load. Trailing slashes are stripped so callers compose `${base}/carts/mine`
// without doubling the separator.
export const serviceUrls: ServiceUrls = serviceUrlsSchema.parse({
  catalogUrl: stripTrailingSlash(import.meta.env.VITE_CATALOG_URL ?? DEV_FALLBACKS.catalog),
  inventoryUrl: stripTrailingSlash(import.meta.env.VITE_INVENTORY_URL ?? DEV_FALLBACKS.inventory),
  ordersUrl: stripTrailingSlash(import.meta.env.VITE_ORDERS_URL ?? DEV_FALLBACKS.orders),
  identityUrl: stripTrailingSlash(import.meta.env.VITE_IDENTITY_URL ?? DEV_FALLBACKS.identity),
});
