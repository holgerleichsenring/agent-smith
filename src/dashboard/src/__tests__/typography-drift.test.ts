import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// p0217: typography drift guard. The dashboard uses a fixed dense type scale
// (dsh-* rungs defined in app/globals.css). Raw `text-[Npx]` sizes are banned
// so a future scale change is one edit, not a grep-and-replace.

const srcDir = join(dirname(fileURLToPath(import.meta.url)), "..");

const DEFINED_TOKENS = ["dsh-h1", "dsh-h2", "dsh-h3", "dsh-body", "dsh-mono", "dsh-label"];

function sourceFiles(): string[] {
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter((d) => d.isFile() && /\.(ts|tsx)$/.test(d.name))
    .map((d) => join(d.parentPath, d.name))
    .filter((p) => !p.includes("__tests__"));
}

describe("Typography drift", () => {
  it("Typography_NoRawPixelTextSizesRemain", () => {
    const offenders: string[] = [];
    for (const file of sourceFiles()) {
      const content = readFileSync(file, "utf8");
      const matches = content.match(/text-\[[0-9.]+px\]/g);
      if (matches) offenders.push(`${file}: ${matches.join(", ")}`);
    }
    expect(offenders, `raw text-[Npx] sizes must use a dsh-* token:\n${offenders.join("\n")}`).toEqual([]);
  });

  it("Typography_EverySurfaceUsesDshToken", () => {
    const css = readFileSync(join(srcDir, "app", "globals.css"), "utf8");
    const declared = new Set(
      [...css.matchAll(/@utility\s+(dsh-[a-z0-9-]+)\s*\{/g)].map((m) => m[1]),
    );

    // Every rung the scale promises is wired.
    for (const token of DEFINED_TOKENS) {
      expect(declared.has(token), `globals.css must define @utility ${token}`).toBe(true);
    }

    // No surface references a dsh-* token that the scale does not define.
    const unknown = new Set<string>();
    for (const file of sourceFiles()) {
      const content = readFileSync(file, "utf8");
      for (const m of content.matchAll(/\bdsh-[a-z0-9-]+/g)) {
        if (!declared.has(m[0])) unknown.add(m[0]);
      }
    }
    expect([...unknown], `undefined dsh-* tokens referenced in source: ${[...unknown].join(", ")}`).toEqual([]);
  });
});
