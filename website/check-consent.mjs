// Proves the analytics and imprint gates. The committed _site is checked against the committed site.json;
// then copies of website/ (in .consent-check-*, inside website/) are built without site.legal, with a sample one without and with an address, and
// checked too, link check included. Analytics off: nothing in the output names Google's tag. Analytics on:
// every page carries the consent dialog, the privacy page renders, and only consent.js references gtag.
// The imprint and its footer link exist only when site.legal has an address.
import { cpSync, existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, symlinkSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";
import { legalState } from "./eleventy.config.js";

const here = dirname(fileURLToPath(import.meta.url));
const SAMPLE_LEGAL = { name: "Sample Controller", address: "Sample Street 1\n12345 Sample City", email: "privacy@example.org" };

const files = (dir) => readdirSync(dir).flatMap((name) => {
  const full = join(dir, name);
  return statSync(full).isDirectory() ? files(full) : [full];
});

function check(root, data, label) {
  const { analytics, imprint } = legalState(data);
  const failures = [];
  const all = files(root);
  const pages = all.filter((f) => f.endsWith(".html"));
  const rel = (f) => f.slice(root.length);
  const html = (f) => readFileSync(f, "utf8");
  const sitemap = readFileSync(join(root, "sitemap.xml"), "utf8");
  const naming = all.filter((f) => /googletagmanager|gtag/.test(readFileSync(f, "utf8"))).map(rel);
  const expect = (cond, message) => { if (!cond) failures.push(message); };

  if (analytics) {
    expect(naming.join() === "/consent.js", `gtag must appear only in /consent.js, found in: ${naming.join(", ") || "nothing"}`);
    for (const page of pages) {
      for (const mark of ['id="consent" role="dialog"', 'src="/consent.js" defer', 'href="/privacy/"', "data-consent-settings"]) {
        expect(html(page).includes(mark), `${rel(page)} lacks ${mark}`);
      }
    }
    const privacy = join(root, "privacy/index.html");
    expect(existsSync(privacy) && html(privacy).includes(data.legal.email), "/privacy/ not rendered with the controller's email");
    expect(sitemap.includes("/privacy</loc>"), "sitemap lacks /privacy");
  } else {
    expect(naming.length === 0, `Google's tag referenced in: ${naming.join(", ")}`);
    for (const path of ["consent.js", "privacy/index.html"]) expect(!existsSync(join(root, path)), `/${path} rendered`);
    for (const page of pages) for (const mark of ['id="consent"', 'href="/privacy/']) expect(!html(page).includes(mark), `${rel(page)} has ${mark}`);
    expect(!sitemap.includes("/privacy</loc>"), "sitemap lists /privacy");
  }

  const imprintPage = join(root, "imprint/index.html");
  if (imprint) {
    expect(existsSync(imprintPage) && html(imprintPage).includes(data.legal.name), "/imprint/ not rendered with the provider");
    for (const page of pages) expect(html(page).includes('href="/imprint/"'), `${rel(page)} lacks the Imprint link`);
    expect(sitemap.includes("/imprint</loc>"), "sitemap lacks /imprint");
  } else {
    expect(!existsSync(imprintPage), "/imprint/ rendered without a postal address");
    for (const page of pages) expect(!html(page).includes('href="/imprint/"'), `${rel(page)} links /imprint/`);
    expect(!sitemap.includes("/imprint</loc>"), "sitemap lists /imprint");
  }

  const name = `check-consent (${label}: analytics ${analytics ? "on" : "off"}, imprint ${imprint ? "on" : "off"})`;
  if (failures.length) {
    console.error(`${name}: ${failures.length} failure(s)`);
    for (const line of failures) console.error(`  ${line}`);
    process.exit(1);
  }
  console.log(`${name}: ${pages.length} pages pass`);
}

function buildFixture(data, label, expectedLine) {
  const work = mkdtempSync(join(here, ".consent-check-"));
  try {
    for (const entry of readdirSync(here)) {
      if (entry === "node_modules" || entry === "_site" || entry.startsWith(".consent-check-")) continue;
      cpSync(join(here, entry), join(work, entry), { recursive: true });
    }
    symlinkSync(join(here, "node_modules"), join(work, "node_modules"), "dir");
    writeFileSync(join(work, "src/_data/site.json"), JSON.stringify(data, null, 2));
    const out = execFileSync(process.execPath, [join(here, "node_modules/@11ty/eleventy/cmd.cjs"), "--quiet"], { cwd: work, encoding: "utf8" });
    if (!out.includes(expectedLine)) {
      console.error(`check-consent (${label}): the build did not print "${expectedLine}"`);
      process.exit(1);
    }
    execFileSync(process.execPath, [join(here, "check-links.mjs"), join(work, "_site")], { stdio: "inherit" });
    check(join(work, "_site"), data, label);
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

const site = JSON.parse(readFileSync(join(here, "src/_data/site.json"), "utf8"));
check(join(here, "_site"), site, "committed");

const { legal, ...withoutLegal } = site;
buildFixture(withoutLegal, "fixture without site.legal", "analytics off: legal details missing in site.json");
buildFixture({ ...site, legal: { name: SAMPLE_LEGAL.name, email: SAMPLE_LEGAL.email } }, "fixture without an address", "imprint off: no postal address in site.json");
buildFixture({ ...site, legal: SAMPLE_LEGAL }, "fixture with an address", "analytics on:");
