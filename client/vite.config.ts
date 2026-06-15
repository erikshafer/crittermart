/// <reference types="vitest/config" />
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// CritterMart storefront build + dev config.
//
// **No dev-server proxy** — and that is the deliberate divergence from the CritterBids sibling
// (ADR 018). CritterBids has one API host and used a Vite proxy to stay same-origin in dev, avoiding
// CORS entirely. CritterMart cannot reuse that benefit: with three services and no BFF (ADR 006),
// production CORS is committed regardless, so a dev proxy would only *hide* the cross-origin boundary
// the OpenTelemetry trace demo depends on. The SPA therefore issues genuine cross-origin requests in
// every environment, reading each service's base URL from import.meta.env.VITE_*_URL (src/config.ts) —
// injected by Aspire's AddViteApp in orchestration, falling back to the launchSettings ports for a
// standalone `npm run dev`.
//
// The dev server is pinned to 5173 so the origin is deterministic: the AppHost injects exactly this
// origin into each service's Cors:AllowedOrigins, and it is the value AddFrontendCors already falls
// back to in Development.
export default defineConfig({
  base: "/",
  plugins: [
    react(),
    // Tailwind v4's native Vite plugin (ADR 015) — no PostCSS config, no tailwind.config.js;
    // theme + content scanning are driven from src/index.css via the @import "tailwindcss" directive.
    tailwindcss(),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: true,
  },
});
