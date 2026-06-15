/// <reference types="vite/client" />

// The three Aspire-injected service base URLs (ADR 018). Typed here so a missing/mistyped key is a
// compile error, but the runtime source of truth is the Zod parse in src/config.ts — these are
// `string | undefined` because in a standalone `npm run dev` (no Aspire) they are absent and config.ts
// falls back to the launchSettings ports.
interface ImportMetaEnv {
  readonly VITE_CATALOG_URL?: string;
  readonly VITE_INVENTORY_URL?: string;
  readonly VITE_ORDERS_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
