import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-09-17-042ee, found in use: `text-muted` was written where the token is `mute`.
// Tailwind emits NOTHING for an unknown utility, so the build stayed green, every test
// stayed green, and the lines rendered in the primary tone instead of de-emphasised. A
// colour that silently does nothing is the one kind of styling mistake nothing else catches.
//
// The scan is components/dialog and nothing wider, as the palette scan is: that one bans a
// RAW palette class, this one bans a token name the theme does not define. Between them, a
// colour under components/dialog is either a defined token or a failure.

const dialogDir = join(dirname(fileURLToPath(import.meta.url)), "..");

const PREFIX =
  "bg|text|border(?:-[trblxyse])?|ring(?:-offset)?|outline|divide|from|via|to|fill|stroke|accent|caret|decoration|placeholder";
// Two characters or more, so a bare direction (`border-b`) is not read as a colour.
const VALUE = "[a-z][a-z0-9]+(?:-[a-z0-9]+)*";
const UTILITY = new RegExp(`(?<![\\w-])(?:${PREFIX})-(${VALUE})\\b`, "g");

/** Values these prefixes carry that are not colours at all, or are CSS-wide keywords. */
const NOT_A_COLOUR = new Set([
  "current", "transparent", "inherit", "none", "auto",
  "left", "center", "right", "justify", "start", "end",
  "wrap", "nowrap", "balance", "pretty", "ellipsis", "clip",
  "solid", "dashed", "dotted", "double", "hidden", "collapse", "separate",
  "cover", "contain", "fixed", "local", "scroll", "repeat", "no-repeat",
]);

// DESIGN.md's frontmatter is the one source the tokens are generated from
// (tools/build-tokens.mjs). The generated tokens.json is not in the repository, and the
// gate runs the tests before the build that writes it, so the scan reads the source.
const designPath = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..", "..", "..", "DESIGN.md");
const colours = readFileSync(designPath, "utf8").split(/\r?\n/);
const opened = colours.findIndex((line) => line === "colors:");
const DEFINED = new Set(
  colours
    .slice(opened + 1)
    .slice(0, colours.slice(opened + 1).findIndex((line) => line.trim().length > 0 && !line.startsWith("  ")))
    .map((line) => line.trim().split(":")[0])
    .filter((name) => name.length > 0),
);

/** Every colour utility in `content` whose value is neither a defined token nor a keyword. */
export function unknownColours(content: string): string[] {
  return [...content.matchAll(UTILITY)]
    .filter((m) => !DEFINED.has(m[1]) && !NOT_A_COLOUR.has(m[1]))
    .map((m) => m[0]);
}

/** Every component file under components/dialog, however deep; its tests are not components. */
function componentFiles(dir: string = dialogDir): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "__tests__" ? [] : componentFiles(path);
    return entry.isFile() && /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

describe("Dialog colour tokens", () => {
  it("DialogComponents_EveryColourUtility_NamesADefinedToken", () => {
    const files = componentFiles();
    expect(files.length).toBeGreaterThan(5);

    const offenders = files.flatMap((file) =>
      unknownColours(readFileSync(file, "utf8")).map((hit) => `${file}: ${hit}`));

    expect(offenders, `colour utilities no token defines:\n${offenders.join("\n")}`).toEqual([]);
  });

  // Proves the rule bites without waiting for someone to write the violation again.
  it("Rule_HasTeeth_AMisspeltTokenIsFlaggedAndTheRealOneIsNot", () => {
    expect(unknownColours('className="text-muted bg-canvas-soft"')).toEqual(["text-muted"]);
    expect(unknownColours('className="text-mute border-b border-t-transparent text-left"')).toEqual([]);
    expect(DEFINED.has("mute")).toBe(true);
    expect(DEFINED.has("muted")).toBe(false);
  });
});
