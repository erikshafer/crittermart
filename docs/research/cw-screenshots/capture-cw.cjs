// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CritterWatch screenshot automation (CW-TELEMETRY SPIKE — research/cw-telemetry-spike).
//
//  Drives a headless Chromium tour of the CritterWatch console and writes a full set of full-page
//  screenshots for the JasperFx (Jeremy/Babu) UI/UX feedback packet. Built so the "now-lit" console
//  (async daemon + broadcast + poison path running under Cw__Telemetry=true) can be captured
//  repeatably — re-run it after any traffic change and the evidence regenerates.
//
//  WHY a driven browser (not URL fetches): CW is an Element-Plus (Vue) SPA. Every server path
//  returns the same shell and the app is client-routed, so the surfaces only exist once a real
//  browser has rendered them. The service/projection pickers are Element-Plus <el-select>s whose
//  readonly <input> is covered by a placeholder span — you must click the .el-select__wrapper and
//  pick a .el-select-dropdown__item, not the input. Stepper option labels are name+lifecycle with
//  no space (e.g. "StockLevelInline").
//
//  RUN (PowerShell, from anywhere):
//    $env:NODE_PATH = "C:\Code\crittermart\client\node_modules"   # reuse the SPA's Playwright + chromium
//    $env:CW_BASE   = "http://localhost:5104"                      # Aspire proxy of critterwatch-console
//    $env:CW_OUT    = "C:\Code\crittermart\docs\research\cw-screenshots\shots"
//    node C:\Code\crittermart\docs\research\cw-screenshots\capture-cw.cjs
//
//  Prereqs: the AppHost stack is up with Cw__Telemetry=true, and traffic has run
//  (docs/demo-traffic.ps1 -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7) so the surfaces have data.
// ════════════════════════════════════════════════════════════════════════════════════════════════
const { chromium } = require("@playwright/test");
const fs = require("fs");
const path = require("path");

const BASE = process.env.CW_BASE || "http://localhost:5104";
const OUT = process.env.CW_OUT || ".";
const VIEWPORT = { width: 2000, height: 1200 }; // wide enough that the projection table isn't clipped

// Top-level surfaces, by the client route map discovered on the console.
const ROUTES = [
  ["00-dashboard", "/"],
  ["01-services", "/services"],
  ["02-topology", "/topology"],
  ["03-projections", "/projections"],
  ["04-durability", "/durability"],
  ["05-listeners", "/listeners"],
  ["06-events", "/events"],
  ["07-explorer", "/explorer"],
  ["08-scheduled", "/scheduled"],
  ["09-dead-letters", "/dlq"],
  ["10-timeline", "/timeline"],
  ["11-audit-log", "/audit-log"],
  ["12-store-inspector", "/raw"],
];

const EXPLORER_TABS = [
  "Recent Streams",
  "Stream Events",
  "Projection Statuses",
  "Projection Stepper",
  "Rehydrate Aggregate",
];

const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const manifest = [];

async function shoot(page, file, note) {
  await page.screenshot({ path: path.join(OUT, file), fullPage: true });
  manifest.push({ file, note });
  console.log(`  ${file.padEnd(40)} ${note}`);
}

// Switch the explorer's service <el-select> (the one in the content header at y≈88; index 0 is the
// global "All Services" header select). Returns true if an option was clicked.
async function selectService(page, to) {
  const wrappers = page.locator(".el-select__wrapper");
  const cnt = await wrappers.count();
  let chosen = null;
  for (let i = 0; i < cnt; i++) {
    const b = await wrappers.nth(i).boundingBox().catch(() => null);
    if (b && b.y > 70 && b.y < 160) { chosen = wrappers.nth(i); break; }
  }
  await (chosen || wrappers.nth(1)).click();
  await page.waitForTimeout(600);
  await page.locator(".el-select-dropdown__item:visible", { hasText: new RegExp(`^${to}$`) }).first().click();
  await page.waitForTimeout(2200);
}

async function captureExplorerTabs(page, service) {
  await page.goto(BASE + "/explorer", { waitUntil: "load", timeout: 30000 });
  await page.waitForTimeout(2500);
  await selectService(page, service);
  for (const tab of EXPLORER_TABS) {
    try {
      await page.getByText(tab, { exact: true }).first().click({ timeout: 6000 });
      await page.waitForTimeout(2200);
      await shoot(page, `ex-${slug(service)}-${slug(tab)}.png`, `Explorer · ${service} · ${tab}`);
    } catch (e) {
      console.log(`  SKIP ${service}/${tab} — ${e.message.split("\n")[0]}`);
    }
  }
}

// Drive the Projection Stepper for one projection + stream id and capture configured/after-run.
async function driveStepper(page, service, projection, streamId, tag, note) {
  await page.goto(BASE + "/explorer", { waitUntil: "load", timeout: 30000 });
  await page.waitForTimeout(2500);
  await selectService(page, service);
  await page.getByText("Projection Stepper", { exact: true }).first().click();
  await page.waitForTimeout(1500);

  await page.locator(".el-select__wrapper", { hasText: "Select a projection" }).first().click();
  await page.waitForTimeout(500);
  // Option labels are name+lifecycle concatenated; anchor the suffix so "StockLevel" doesn't also
  // match "StockLevelView".
  await page.locator(".el-select-dropdown__item:visible", { hasText: new RegExp(`^${projection}(Inline|Async)$`) }).first().click();
  await page.waitForTimeout(500);

  await page.getByPlaceholder("Enter a stream id to step through").fill(streamId);
  await shoot(page, `st-${tag}-1-configured.png`, `Stepper configured · ${note}`);

  await page.getByRole("button", { name: /Run Projection/i }).click();
  await page.waitForTimeout(2800);
  await shoot(page, `st-${tag}-2-after-run.png`, `Stepper after Run · ${note}`);
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT });

  console.log("Top-level surfaces:");
  for (const [name, route] of ROUTES) {
    await page.goto(BASE + route, { waitUntil: "load", timeout: 30000 });
    await page.waitForTimeout(3000);
    await shoot(page, `${name}.png`, `Route ${route}`);
  }

  console.log("Event Store Explorer per service:");
  await captureExplorerTabs(page, "Orders");
  await captureExplorerTabs(page, "Inventory");

  console.log("Projection Stepper (driven):");
  // The centerpiece: an immutable record aggregate that fails to rehydrate (every row red).
  await driveStepper(page, "Inventory", "StockLevel", "crit-001",
    "stocklevel", "Inventory/StockLevel — immutable-record rehydrate failure");
  // The contrast: a fan-out multi-stream projection (returns empty when stepped by Stream id).
  await driveStepper(page, "Orders", "ProductSalesLeaderboard", "crit-001",
    "leaderboard", "Orders/ProductSalesLeaderboard — multi-stream, Stream source");

  fs.writeFileSync(path.join(OUT, "manifest.json"), JSON.stringify(manifest, null, 2));
  await browser.close();
  console.log(`\nDone. ${manifest.length} screenshots + manifest.json in ${OUT}`);
})().catch((e) => { console.error("CAPTURE FAILED:", e); process.exit(1); });
