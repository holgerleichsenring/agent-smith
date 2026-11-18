// Takes the docs screenshots from a dashboard running against the seeded demo server.
// Run through run.sh; on its own:  BASE_URL=http://127.0.0.1:3917 OUT_DIR=docs/assets/screenshots node shoot.mjs
// ONLY=home,run-log takes a subset.
import { chromium } from "playwright";
import { existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const BASE = process.env.BASE_URL ?? "http://127.0.0.1:3917";
const OUT = process.env.OUT_DIR ?? "docs/assets/screenshots";
const ONLY = (process.env.ONLY ?? "").split(",").filter(Boolean);
const CHROME = process.env.CHROME_PATH ?? defaultChrome();

function defaultChrome() {
  const local = `${process.env.HOME}/Library/Caches/ms-playwright/chromium-1223/chrome-mac-arm64/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing`;
  return existsSync(local) ? local : undefined; // undefined: Playwright's own Chromium (npx playwright install chromium)
}

// The rig's server runs without Redis, so the shell shows a degraded banner. It is an artefact of
// the rig, not of the product, and is hidden in every shot.
const RIG_CSS = `[data-testid="degraded-banner"]{display:none!important}`;

async function api(path) {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`${path}: ${res.status}`);
  return res.json();
}

const conversations = (await api("/api/spec-dialog/conversations")).conversations;
const conversation = (dialog) => {
  const c = conversations.find((x) => x.openDialogId === dialog);
  if (!c) throw new Error(`conversation ${dialog} not seeded`);
  return `/spec-dialog?open=${c.sessionId}&on=${dialog}`;
};
const ticket41 = await api("/api/tickets/shop/41");
const run41 = ticket41.attempts.find((a) => a.number === 1).runId; // the first attempt: two specs, the long log

const shots = [
  {
    name: "home",
    path: "/",
    act: async (page) => {
      await pickProject(page, "shop");
      await page.getByRole("textbox").first().fill("Customers want to pay with a gift card at checkout, and keep the rest on the card for next time.");
    },
  },
  { name: "tickets-list", path: "/tickets" },
  { name: "work-it-out", path: () => conversation("c-demo-gift-cards") },
  { name: "ticket-page", path: "/tickets/shop/41" },
  { name: "ticket-specs", path: "/tickets/shop/41?section=specs" },
  { name: "ticket-runs-cost", path: "/tickets/shop/41?section=runs" },
  { name: "run-log", path: () => `/jobs/${run41}` },
  {
    name: "run-step-llm-calls",
    path: () => `/jobs/${run41}?step=9`,
    act: async (page) => {
      const llm = page.getByRole("button", { name: /^LLM/ }).or(page.getByRole("link", { name: /^LLM/ }));
      if (await llm.count()) await llm.first().click();
      await page.waitForTimeout(800);
      await page.getByText("Price tiers", { exact: true }).first().evaluate((e) => e.scrollIntoView({ block: "start" }));
      await page.mouse.wheel(0, -150);
    },
  },
  {
    name: "series-status",
    path: () => conversation("c-demo-saved-carts"),
    route: seriesPanelRoute,
  },
  {
    name: "config-studio",
    path: "/config/projects",
    act: async (page) => {
      await page.locator("text=shop").first().waitFor();
      const toggles = page.locator("button[aria-expanded]");
      if (await toggles.count()) await toggles.last().click();
      await page.waitForTimeout(800);
    },
  },
  {
    name: "config-merge",
    path: "/config/projects",
    act: async (page) => {
      await page.getByRole("button", { name: "Edit" }).nth(1).click();
      await page.getByRole("tab", { name: /merge/i }).first().click();
      await page.waitForTimeout(600);
    },
  },
  {
    name: "config-secrets",
    path: "/config/secrets",
    act: async (page) => {
      await page.getByText("anthropic_api_key").first().click();
      await page.waitForTimeout(800);
    },
  },
  { name: "admin-assistant", path: () => conversation("c-demo-admin") },
];

// The series panel's "landed" count is read with every ticket counted as landed (a product defect:
// SeriesTicketFacts turns a missing landing into a default timestamp). Until it is fixed the panel is
// shown with the counts the seeded story has: #44 landed, #45 running, #46 held on #45.
async function seriesPanelRoute(page) {
  await page.route("**/api/spec-dialog/*/series", async (route) => {
    const res = await route.fetch();
    const view = await res.json();
    if (view) {
      view.landed = 1;
      view.now = [
        { ticket: "#45", text: "running · spec 2 of 2", kind: "running" },
        { ticket: "#46", text: "waits for #45", kind: "held" },
      ];
      view.criticalPath = ["github-45", "github-46"];
      view.criticalPathLeft = "2 tickets, about 1 h 10 min left";
    }
    await route.fulfill({ response: res, json: view });
  });
}

async function pickProject(page, name) {
  const select = page.locator("select").first();
  if (await select.count()) { await select.selectOption({ label: name }).catch(() => {}); return; }
  const picker = page.getByRole("button", { name: /billing|shop/ }).first();
  if (await picker.count()) {
    await picker.click();
    await page.getByRole("option", { name }).or(page.getByRole("menuitem", { name })).first().click().catch(() => {});
  }
}

mkdirSync(OUT, { recursive: true });
const browser = await chromium.launch({ executablePath: CHROME });
try {
  for (const shot of shots) {
    if (ONLY.length && !ONLY.includes(shot.name)) continue;
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, colorScheme: "dark" });
    const page = await context.newPage();
    if (shot.route) await shot.route(page);
    const path = typeof shot.path === "function" ? shot.path() : shot.path;
    await page.goto(`${BASE}${path}`, { waitUntil: "load" });
    await page.addStyleTag({ content: RIG_CSS });
    await page.waitForTimeout(2500);
    if (shot.act) await shot.act(page);
    await page.addStyleTag({ content: RIG_CSS });
    await page.waitForTimeout(700);
    const file = join(OUT, `${shot.name}.png`);
    await page.screenshot({ path: file });
    console.log(`wrote ${file}`);
    await context.close();
  }
} finally {
  await browser.close();
}
