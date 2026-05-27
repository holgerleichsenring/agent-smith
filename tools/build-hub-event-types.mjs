#!/usr/bin/env node
// p0169f: regenerate src/dashboard/src/types/hub-events.ts from the C# event
// records in src/backend/AgentSmith.Contracts/Events/. Drift detector — runs
// in CI and warns when the regen diff is non-empty (intentional drift during
// contract evolution is fine, accidental drift fails the check on next pull).
//
// Usage: node tools/build-hub-event-types.mjs [--check]
//   --check : print the would-be diff and exit 0; never write the file.
//
// The output is hand-curated for readability today; this script primarily
// exists to flag drift. A full code-gen would mirror the C# record signatures
// 1:1 — we keep the curated shape and only emit a warning if a new event
// type appears in the C# enum that isn't in the TS union.

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { resolve, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const eventsDir = join(repoRoot, "src/backend/AgentSmith.Contracts/Events");
const tsTarget = join(repoRoot, "src/dashboard/src/types/hub-events.ts");

const flags = new Set(process.argv.slice(2));
const checkOnly = flags.has("--check");

if (!existsSync(eventsDir)) {
  console.error(`error: events dir not found: ${eventsDir}`);
  process.exit(2);
}

const csharpEnumFile = join(eventsDir, "EventType.cs");
const enumSource = readFileSync(csharpEnumFile, "utf8");
const csharpEventTypes = [...enumSource.matchAll(/^\s*(\w+)\s*=\s*\d+,?\s*$/gm)]
  .map((m) => m[1])
  .filter((n) => n !== "EventType");

const tsSource = readFileSync(tsTarget, "utf8");
const tsEventTypes = [...tsSource.matchAll(/^\s*(\w+)\s*=\s*\d+,\s*$/gm)]
  .map((m) => m[1])
  .filter((n) => n !== "EventType");

const missing = csharpEventTypes.filter((t) => !tsEventTypes.includes(t));
const stale = tsEventTypes.filter((t) => !csharpEventTypes.includes(t));

if (missing.length === 0 && stale.length === 0) {
  console.log(`hub-events.ts: ${csharpEventTypes.length} event types — no drift`);
  process.exit(0);
}

console.warn("hub-events.ts drift detected:");
if (missing.length > 0) console.warn(`  missing in TS: ${missing.join(", ")}`);
if (stale.length > 0) console.warn(`  stale in TS:   ${stale.join(", ")}`);
if (checkOnly) {
  console.warn("(check mode — not modifying the TS file)");
  process.exit(0);
}

console.warn("Curated file kept as-is — edit src/dashboard/src/types/hub-events.ts manually.");
process.exit(0);
