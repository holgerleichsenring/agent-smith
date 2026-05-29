#!/usr/bin/env node
// p0169f / p0173e: regenerate-and-check drift detector for the dashboard's
// TypeScript mirror of the C# event records under
// src/backend/AgentSmith.Contracts/Events/.
//
// Usage: node tools/build-hub-event-types.mjs [--check]
//   --check : exit 1 on drift; never write the file.
//
// The output is hand-curated for readability; this script primarily flags
// drift. Two layers of comparison:
//
//   (1) enum-level: every entry in the C# `EventType` / `SystemEventType`
//       enum must appear in the TS mirror's matching enum.
//   (2) record-level (p0173e): every public record under Events/ must have
//       a TS interface in the matching `*-events.ts` file. A record added
//       in C# without a TS mirror is treated as drift; a record present in
//       TS but absent in C# is treated as stale.
//
// Both layers must pass before the build is considered clean.

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { resolve, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const eventsDir = join(repoRoot, "src/backend/AgentSmith.Contracts/Events");

const flags = new Set(process.argv.slice(2));
const checkOnly = flags.has("--check");

if (!existsSync(eventsDir)) {
  console.error(`error: events dir not found: ${eventsDir}`);
  process.exit(2);
}

const channels = [
  {
    label: "hub-events.ts",
    enumName: "EventType",
    csharpFile: join(eventsDir, "EventType.cs"),
    tsFile: join(repoRoot, "src/dashboard/src/types/hub-events.ts"),
    csharpBase: "RunEvent",
  },
  {
    label: "system-events.ts",
    enumName: "SystemEventType",
    csharpFile: join(eventsDir, "SystemEventType.cs"),
    tsFile: join(repoRoot, "src/dashboard/src/types/system-events.ts"),
    csharpBase: "SystemEvent",
  },
];

const allRecords = scanRecordsByBase(eventsDir);

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

  if (!compareEnum(channel)) anyDrift = true;
  if (!compareRecords(channel, allRecords)) anyDrift = true;
}

if (anyDrift) {
  if (checkOnly) {
    console.error("event-types drift detected — fail.");
    process.exit(1);
  }
  console.warn("Curated TS files kept as-is — edit the TS mirror manually.");
}

function compareEnum(channel) {
  const enumSource = readFileSync(channel.csharpFile, "utf8");
  const csharpTypes = [...enumSource.matchAll(/^\s*(\w+)\s*=\s*\d+,?\s*$/gm)]
    .map((m) => m[1])
    .filter((n) => n !== channel.enumName);
  const tsSource = readFileSync(channel.tsFile, "utf8");
  const tsEnumBody = extractEnumBody(tsSource, channel.enumName);
  const tsTypes = [...tsEnumBody.matchAll(/^\s*(\w+)\s*=\s*\d+,\s*$/gm)]
    .map((m) => m[1])
    .filter((n) => n !== channel.enumName);

  const missing = csharpTypes.filter((t) => !tsTypes.includes(t));
  const stale = tsTypes.filter((t) => !csharpTypes.includes(t));

  if (missing.length === 0 && stale.length === 0) {
    console.log(`${channel.label} (enum): ${csharpTypes.length} entries — no drift`);
    return true;
  }
  console.warn(`${channel.label} (enum) drift detected:`);
  if (missing.length > 0) console.warn(`  missing in TS: ${missing.join(", ")}`);
  if (stale.length > 0) console.warn(`  stale in TS:   ${stale.join(", ")}`);
  return false;
}

function compareRecords(channel, records) {
  const expected = records.filter((r) => r.base === channel.csharpBase).map((r) => r.name);
  const tsSource = readFileSync(channel.tsFile, "utf8");
  const tsInterfaces = [...tsSource.matchAll(/^\s*export\s+interface\s+(\w+Event)\b/gm)].map(
    (m) => m[1]
  );

  const missing = expected.filter((n) => !tsInterfaces.includes(n));
  const stale = tsInterfaces.filter((n) => !expected.includes(n));

  if (missing.length === 0 && stale.length === 0) {
    console.log(`${channel.label} (records): ${expected.length} records — no drift`);
    return true;
  }
  console.warn(`${channel.label} (records) drift detected:`);
  if (missing.length > 0) console.warn(`  missing in TS: ${missing.join(", ")}`);
  if (stale.length > 0) console.warn(`  stale in TS:   ${stale.join(", ")}`);
  return false;
}

function scanRecordsByBase(dir) {
  // Returns [{ name, base }] for every public record under Events/.
  // Robust against single-line and multi-line record declarations:
  // detects "public sealed record Foo(...) : RunEvent" + variants like
  // ": RunEvent(RunId, EventType.X, Timestamp);" on later lines.
  const files = readdirSync(dir).filter((f) => f.endsWith(".cs"));
  const found = [];
  for (const file of files) {
    const source = readFileSync(join(dir, file), "utf8");
    const recordPattern = /public\s+sealed\s+record\s+(\w+Event)\b[\s\S]*?:\s*(\w+)\s*[\(;{]/gm;
    let match;
    while ((match = recordPattern.exec(source)) !== null) {
      const [, name, baseName] = match;
      if (baseName === "RunEvent" || baseName === "SystemEvent") {
        found.push({ name, base: baseName, file });
      }
    }
  }
  return found;
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

process.exit(anyDrift ? 1 : 0);
