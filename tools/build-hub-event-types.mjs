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

// p0173a: scan both RunEvent (EventType) and SystemEvent (SystemEventType)
// hierarchies. Each is mirrored in its own dashboard file.
const channels = [
  {
    label: "hub-events.ts",
    enumName: "EventType",
    csharpFile: join(eventsDir, "EventType.cs"),
    tsFile: join(repoRoot, "src/dashboard/src/types/hub-events.ts"),
  },
  {
    label: "system-events.ts",
    enumName: "SystemEventType",
    csharpFile: join(eventsDir, "SystemEventType.cs"),
    tsFile: join(repoRoot, "src/dashboard/src/types/system-events.ts"),
  },
];

let anyDrift = false;
for (const channel of channels) {
  if (!existsSync(channel.csharpFile)) {
    console.warn(`skipping ${channel.label}: ${channel.csharpFile} not present`);
    continue;
  }
  if (!existsSync(channel.tsFile)) {
    console.warn(`drift in ${channel.label}: TS mirror missing at ${channel.tsFile}`);
    anyDrift = true;
    continue;
  }
  const enumSource = readFileSync(channel.csharpFile, "utf8");
  const csharpTypes = [...enumSource.matchAll(/^\s*(\w+)\s*=\s*\d+,?\s*$/gm)]
    .map((m) => m[1])
    .filter((n) => n !== channel.enumName);
  const tsSource = readFileSync(channel.tsFile, "utf8");
  // p0173c: scope the regex to the NAMED enum's body so unrelated enums in
  // the same TS file (TicketSkipReason, ConfigFileKind, …) don't trigger
  // false-positive drift reports.
  const tsEnumBody = extractEnumBody(tsSource, channel.enumName);
  const tsTypes = [...tsEnumBody.matchAll(/^\s*(\w+)\s*=\s*\d+,\s*$/gm)]
    .map((m) => m[1])
    .filter((n) => n !== channel.enumName);

  const missing = csharpTypes.filter((t) => !tsTypes.includes(t));
  const stale = tsTypes.filter((t) => !csharpTypes.includes(t));

  if (missing.length === 0 && stale.length === 0) {
    console.log(`${channel.label}: ${csharpTypes.length} event types — no drift`);
    continue;
  }
  anyDrift = true;
  console.warn(`${channel.label} drift detected:`);
  if (missing.length > 0) console.warn(`  missing in TS: ${missing.join(", ")}`);
  if (stale.length > 0) console.warn(`  stale in TS:   ${stale.join(", ")}`);
}

if (anyDrift && !checkOnly) {
  console.warn("Curated files kept as-is — edit the TS mirror manually.");
}

function extractEnumBody(source, enumName) {
  const open = new RegExp(`export\\s+enum\\s+${enumName}\\s*\\{`).exec(source);
  if (!open) return "";
  let depth = 1;
  let i = open.index + open[0].length;
  const start = i;
  while (i < source.length && depth > 0) {
    const ch = source[i];
    if (ch === "{") depth++;
    else if (ch === "}") depth--;
    i++;
  }
  return source.slice(start, i - 1);
}
process.exit(0);
