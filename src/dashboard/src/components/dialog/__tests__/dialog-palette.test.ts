import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-09-17-c7aed: the dialog components are drawn in the dashboard's colour tokens. This is
// a CLASS-NAME SCAN of components/dialog and nothing wider: other surfaces still use the raw
// palette, and a raw colour arriving through a stylesheet or a shared primitive passes it.

const dialogDir = join(dirname(fileURLToPath(import.meta.url)), "..");

const PALETTE =
  "slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose";
const PREFIX =
  "bg|text|border(?:-[trblxyse])?|ring(?:-offset)?|outline|divide|from|via|to|fill|stroke|accent|caret|decoration|placeholder|shadow";
// A named shade, white or black, or an arbitrary colour value in brackets (`bg-[#123456]`,
// `text-[color:var(--x)]`, `border-[rgb(1,2,3)]`).
const VALUE = `(?:${PALETTE})-\\d{2,3}\\b|(?:white|black)\\b|\\[(?:#|color:|rgba?\\(|hsla?\\(|oklch\\()[^\\]]*\\]`;
const RAW_CLASS = new RegExp(`(?<![\\w-])(?:${PREFIX})-(?:${VALUE})`, "g");

/** Every component file under components/dialog, however deep; its tests are not components. */
function componentFiles(dir: string = dialogDir): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "__tests__" ? [] : componentFiles(path);
    return entry.isFile() && /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

describe("Dialog palette", () => {
  it("DialogComponents_ClassNameScan_FindsNoRawPaletteClass", () => {
    const files = componentFiles();
    expect(files.length).toBeGreaterThan(5);

    const offenders = files.flatMap((file) =>
      [...readFileSync(file, "utf8").matchAll(RAW_CLASS)].map((match) => `${file}: ${match[0]}`));

    expect(offenders, `raw palette classes under components/dialog:\n${offenders.join("\n")}`).toEqual([]);
  });
});
