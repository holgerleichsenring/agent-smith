import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-10-09-724c: the shell's scroll container must be the containing block of every absolute
// descendant, or an sr-only label far down a long page extends the document past the viewport.
// jsdom does no layout, so the rule itself is what is pinned.

const globalsCss = join(dirname(fileURLToPath(import.meta.url)), "..", "globals.css");

function shellMainRule(): string {
  const match = readFileSync(globalsCss, "utf8").match(/@utility shell-main\s*\{([^}]*)\}/);
  if (!match) throw new Error("globals.css declares no shell-main utility");
  return match[1];
}

describe("Shell scroll container", () => {
  it("ShellMain_Utility_EstablishesContainingBlock", () => {
    const rule = shellMainRule();

    expect(rule).toMatch(/position:\s*relative/);
    expect(rule).toMatch(/overflow-y:\s*auto/);
  });
});
