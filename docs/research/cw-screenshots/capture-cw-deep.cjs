// ════════════════════════════════════════════════════════════════════════════════════════════════
//  CritterWatch DEEP-ROUND harness (research/cw-telemetry-spike).
//
//  Round one (capture-cw.cjs) was a static full-page photo tour. This deep-round harness DRIVES the
//  console instead of only photographing it, and adds three dimensions the photo tour couldn't reach:
//
//    1. ACCESSIBILITY — injects axe-core (WCAG 2.1 A/AA) on every top-level route and writes a
//       machine-readable violation report (axe-report.json). Also a keyboard/focus probe: Tab through
//       the dashboard and record whether the focused control shows a visible focus ring.
//    2. INTERACTION-LEVEL UX — drives state transitions (idle → run → empty/error) so the three states
//       can be compared side by side, and reproduces the two-service-selector contradiction (header
//       scope vs Explorer scope) as a single captured frame.
//    3. RESPONSIVE / THEME — re-shoots the highest-value surfaces at narrow viewports (1024, 768) to
//       document clipping/reflow, and once under prefers-color-scheme:dark to see if a dark theme
//       exists and how the all-red error states read in it.
//    4. DEEP-LINKING — selects a service in the Explorer, reloads, and records whether the scope
//       survives (i.e. is any view state reflected in the URL / persisted?).
//
//  Element-Plus selector notes carried from round one (still true on alpha.3): every server path
//  returns the same Vue shell, so surfaces only exist after a real browser renders them. Service /
//  projection pickers are <el-select>s whose readonly <input> is covered by a placeholder span — click
//  the .el-select__wrapper and pick a .el-select-dropdown__item, never the input. The global header
//  picker is the first .el-select__wrapper (index 0); a page-level picker sits lower (y≈70–160).
//  Stepper option labels are name+lifecycle concatenated with no space (e.g. "StockLevelInline").
//
//  RUN (PowerShell):
//    $env:NODE_PATH = "C:\Code\crittermart\client\node_modules"
//    $env:CW_BASE   = "http://localhost:5104"
//    $env:CW_OUT    = "C:\Code\crittermart\docs\research\cw-screenshots\deep"
//    node C:\Code\crittermart\docs\research\cw-screenshots\capture-cw-deep.cjs
//
//  Prereqs: the AppHost stack is up with Cw__Telemetry=true and traffic has run
//  (docs/demo-traffic.ps1 -Continuous -LinesPerOrder 2 -MaxQuantity 3 -PoisonEvery 7).
// ════════════════════════════════════════════════════════════════════════════════════════════════
const { chromium } = require("@playwright/test");
const fs = require("fs");
const path = require("path");

const BASE = process.env.CW_BASE || "http://localhost:5104";
const OUT = process.env.CW_OUT || ".";
const AXE = process.env.CW_AXE ||
  "C:\\Code\\crittermart\\client\\node_modules\\axe-core\\axe.min.js";
const WIDE = { width: 2000, height: 1200 };

// Top-level surfaces by the client route map (same set round one walked).
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

// The surfaces worth re-shooting narrow — the ones round one flagged for horizontal clipping
// (Projections, DLQ filter wall) plus the dashboard and the Explorer's dense tables.
const NARROW_ROUTES = [
  ["00-dashboard", "/"],
  ["03-projections", "/projections"],
  ["04-durability", "/durability"],
  ["07-explorer", "/explorer"],
  ["09-dead-letters", "/dlq"],
];
const NARROW_WIDTHS = [1024, 768];

const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const manifest = [];
const axeReport = [];

async function shoot(page, file, note) {
  await page.screenshot({ path: path.join(OUT, file), fullPage: true });
  manifest.push({ file, note });
  console.log(`  ${file.padEnd(46)} ${note}`);
}

// Run axe-core against the current page and fold the result into axeReport. Tags scope it to the
// WCAG 2.1 A/AA rules an operator console is realistically held to.
async function axeScan(page, surface, route) {
  try {
    await page.addScriptTag({ path: AXE });
    const result = await page.evaluate(async () => {
      // eslint-disable-next-line no-undef
      return await window.axe.run(document, {
        runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
      });
    });
    const violations = result.violations.map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      nodes: v.nodes.length,
      sample: v.nodes.slice(0, 3).map((n) => (n.target || []).join(" ")),
    }));
    axeReport.push({ surface, route, violationCount: violations.length, violations });
    const total = violations.reduce((a, v) => a + v.nodes, 0);
    console.log(`    axe ${surface.padEnd(20)} ${violations.length} rule(s), ${total} node(s)`);
  } catch (e) {
    axeReport.push({ surface, route, error: e.message.split("\n")[0] });
    console.log(`    axe ${surface} FAILED — ${e.message.split("\n")[0]}`);
  }
}

// Switch a page-level service <el-select> (the content-header one at y≈70–160; index 0 is the global
// "All Services" header select). Returns the option text actually clicked, or null.
async function selectPageService(page, to) {
  const wrappers = page.locator(".el-select__wrapper");
  const cnt = await wrappers.count();
  let chosen = null;
  for (let i = 0; i < cnt; i++) {
    const b = await wrappers.nth(i).boundingBox().catch(() => null);
    if (b && b.y > 70 && b.y < 160) { chosen = wrappers.nth(i); break; }
  }
  await (chosen || wrappers.nth(1)).click();
  await page.waitForTimeout(600);
  const opt = page.locator(".el-select-dropdown__item:visible", { hasText: new RegExp(`^${to}$`) }).first();
  if (await opt.count()) { await opt.click(); await page.waitForTimeout(2000); return to; }
  await page.keyboard.press("Escape");
  return null;
}

// Set the GLOBAL header picker (index 0). Used for the two-selector conflict repro.
async function selectHeaderService(page, to) {
  await page.locator(".el-select__wrapper").nth(0).click();
  await page.waitForTimeout(600);
  const opt = page.locator(".el-select-dropdown__item:visible", { hasText: new RegExp(`^${to}$`) }).first();
  if (await opt.count()) { await opt.click(); await page.waitForTimeout(1500); return to; }
  await page.keyboard.press("Escape");
  return null;
}

// ── Probe: keyboard / focus visibility on the dashboard ────────────────────────────────────────────
// Tab a handful of times and record, for each stop, the focused element and whether the browser is
// drawing a visible focus indicator (outline width or box-shadow). A console that fails this leaves
// keyboard-only operators with no idea where they are.
async function focusProbe(page) {
  await page.goto(BASE + "/", { waitUntil: "load", timeout: 30000 });
  await page.waitForTimeout(2500);
  const stops = [];
  for (let i = 0; i < 10; i++) {
    await page.keyboard.press("Tab");
    await page.waitForTimeout(180);
    const info = await page.evaluate(() => {
      const el = document.activeElement;
      if (!el || el === document.body) return { tag: "(none)", visibleFocus: false };
      const cs = getComputedStyle(el);
      const outline = parseFloat(cs.outlineWidth) || 0;
      const ring = cs.boxShadow && cs.boxShadow !== "none";
      return {
        tag: el.tagName.toLowerCase(),
        cls: (el.className || "").toString().slice(0, 60),
        text: (el.textContent || "").trim().slice(0, 30),
        visibleFocus: (outline > 0 && cs.outlineStyle !== "none") || ring,
        outlineWidth: cs.outlineWidth,
        boxShadow: ring ? "yes" : "no",
      };
    });
    stops.push({ stop: i + 1, ...info });
  }
  await shoot(page, "a11y-focus-after-tabbing.png", "Dashboard after 10× Tab — is the focused control visibly ringed?");
  fs.writeFileSync(path.join(OUT, "focus-probe.json"), JSON.stringify(stops, null, 2));
  const withRing = stops.filter((s) => s.visibleFocus).length;
  console.log(`    focus: ${withRing}/${stops.length} tab stops show a visible focus ring`);
}

// ── Probe: the two service selectors can disagree (round-one entry 3) ───────────────────────────────
// Set the global header scope to Catalog, then set the Explorer's own picker to Inventory, and capture
// the single frame where the header says one thing and the page body another.
async function twoSelectorConflict(page) {
  await page.goto(BASE + "/explorer", { waitUntil: "load", timeout: 30000 });
  await page.waitForTimeout(2500);
  const header = await selectHeaderService(page, "Catalog");
  const pagePick = await selectPageService(page, "Inventory");
  await shoot(page, "ux-two-selector-conflict.png",
    `Header scope='${header}' vs Explorer scope='${pagePick}' — contradictory scopes in one view`);
  return { header, pagePick };
}

// ── Probe: deep-linking / state persistence ────────────────────────────────────────────────────────
// Select Inventory in the Explorer, record the URL, reload, and report whether the scope survived.
async function deepLinkProbe(page) {
  await page.goto(BASE + "/explorer", { waitUntil: "load", timeout: 30000 });
  await page.waitForTimeout(2500);
  await selectPageService(page, "Inventory");
  const urlAfterSelect = page.url();
  await page.reload({ waitUntil: "load" });
  await page.waitForTimeout(2500);
  const scopeAfterReload = await page.evaluate(() => {
    // Best-effort: read the visible text of the page-level select trigger.
    const sels = [...document.querySelectorAll(".el-select__wrapper")];
    const lower = sels.find((s) => { const r = s.getBoundingClientRect(); return r.y > 70 && r.y < 160; });
    return (lower || sels[1] || sels[0])?.textContent?.trim().slice(0, 40) || "(unknown)";
  });
  await shoot(page, "ux-deeplink-after-reload.png",
    `Explorer reloaded — selected Inventory, URL was '${urlAfterSelect}', scope after reload: '${scopeAfterReload}'`);
  const urlEncodesScope = /inventory/i.test(urlAfterSelect);
  console.log(`    deep-link: URL ${urlEncodesScope ? "DOES" : "does NOT"} encode the selected service; scope after reload = ${scopeAfterReload}`);
  return { urlAfterSelect, urlEncodesScope, scopeAfterReload };
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();

  // ── Pass 1: wide light + axe on every route ──────────────────────────────────────────────────────
  console.log("Pass 1 — wide light capture + axe scan:");
  const page = await browser.newPage({ viewport: WIDE });
  for (const [name, route] of ROUTES) {
    await page.goto(BASE + route, { waitUntil: "load", timeout: 30000 });
    await page.waitForTimeout(3000);
    await shoot(page, `wide/${name}.png`, `Route ${route} (wide light)`);
    await axeScan(page, name, route);
  }

  // ── Pass 2: interaction probes ───────────────────────────────────────────────────────────────────
  console.log("Pass 2 — interaction probes:");
  await focusProbe(page);
  const conflict = await twoSelectorConflict(page);
  const deepLink = await deepLinkProbe(page);
  await page.close();

  // ── Pass 3: narrow viewports ─────────────────────────────────────────────────────────────────────
  console.log("Pass 3 — narrow viewports:");
  for (const w of NARROW_WIDTHS) {
    const np = await browser.newPage({ viewport: { width: w, height: 900 } });
    for (const [name, route] of NARROW_ROUTES) {
      await np.goto(BASE + route, { waitUntil: "load", timeout: 30000 });
      await np.waitForTimeout(2800);
      await shoot(np, `narrow/${w}-${name}.png`, `Route ${route} @ ${w}px wide`);
    }
    await np.close();
  }

  // ── Pass 4: dark mode ────────────────────────────────────────────────────────────────────────────
  console.log("Pass 4 — dark mode (prefers-color-scheme: dark):");
  const dark = await browser.newPage({ viewport: WIDE, colorScheme: "dark" });
  for (const [name, route] of [["00-dashboard", "/"], ["03-projections", "/projections"], ["09-dead-letters", "/dlq"]]) {
    await dark.goto(BASE + route, { waitUntil: "load", timeout: 30000 });
    await dark.waitForTimeout(2800);
    await shoot(dark, `dark/${name}.png`, `Route ${route} (prefers-color-scheme: dark)`);
  }
  await dark.close();

  fs.writeFileSync(path.join(OUT, "axe-report.json"), JSON.stringify(axeReport, null, 2));
  fs.writeFileSync(path.join(OUT, "probes.json"), JSON.stringify({ conflict, deepLink }, null, 2));
  fs.writeFileSync(path.join(OUT, "manifest.json"), JSON.stringify(manifest, null, 2));
  await browser.close();

  const totalViol = axeReport.reduce((a, r) => a + (r.violationCount || 0), 0);
  console.log(`\nDone. ${manifest.length} screenshots, ${totalViol} axe rule-hits across ${axeReport.length} surfaces, in ${OUT}`);
})().catch((e) => { console.error("DEEP CAPTURE FAILED:", e); process.exit(1); });
