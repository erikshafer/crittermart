import { expect, test } from "@playwright/test";

// Smoke test for the seeder + storefront startup sequence.
//
// The invariant under test: when the Vite dev server becomes available (held
// back by AppHost's WaitForCompletion(seeder)), all three demo products must
// already be in the catalog. A cold first load should never hit the empty-state
// or show only a subset of products.
//
// Three products seed the demo:
//   crit-001    → "Cosmic Critter Plush"  (happy-path order)
//   crit-rare   → "Rare Critter"          (insufficient-stock cancel)
//   crit-deluxe → "Deluxe Critter"        (payment-decline cancel)

test("all three seeded products appear on first load — no refresh needed", async ({
  page,
}) => {
  await page.goto("/");

  // Each product renders its name as an <h2> inside the grid — wait for all
  // three to be visible. Playwright's web-first assertions retry automatically,
  // covering the TanStack Query flight time, but NOT a missing-seeder race.
  await expect(
    page.getByRole("heading", { name: "Cosmic Critter Plush" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Rare Critter" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Deluxe Critter" }),
  ).toBeVisible();

  // Belt-and-suspenders: confirm neither the empty state nor the loading state
  // is still showing alongside the products.
  await expect(page.getByText("No products are available yet.")).not.toBeVisible();
  await expect(page.getByText("Loading products…")).not.toBeVisible();
});
