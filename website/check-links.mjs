// Fails when a page in _site references a local file that the build did not write.
// External URLs and same-page anchors are out of scope; a root-relative path must
// resolve to a file, a directory index, or a clean URL (vercel.json cleanUrls).
import { readdirSync, readFileSync, existsSync, statSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), "_site");

function htmlFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) return htmlFiles(full);
    return name.endsWith(".html") ? [full] : [];
  });
}

function resolves(target, fromFile) {
  const path = target.split("#")[0].split("?")[0];
  if (path === "") return true;
  const base = path.startsWith("/") ? join(siteRoot, path) : join(dirname(fromFile), path);
  if (existsSync(base) && statSync(base).isFile()) return true;
  if (existsSync(join(base, "index.html"))) return true;
  return existsSync(`${base}.html`);
}

const missing = [];
for (const file of htmlFiles(siteRoot)) {
  const html = readFileSync(file, "utf8");
  for (const [, target] of html.matchAll(/\s(?:href|src)="([^"]+)"/g)) {
    if (/^(?:[a-z]+:|\/\/|#)/i.test(target)) continue;
    if (!resolves(target, file)) missing.push(`${file.slice(siteRoot.length)} -> ${target}`);
  }
}

if (missing.length > 0) {
  console.error(`check-links: ${missing.length} missing target(s)`);
  for (const line of missing) console.error(`  ${line}`);
  process.exit(1);
}
console.log("check-links: every local target resolves");
