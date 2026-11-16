import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// 2026-11-16-0440a: the dashboard's dark theme. DESIGN.md's frontmatter is the one source of the
// app-* colours; globals.css mirrors them by hand, so this test holds the two equal and checks
// that every text colour on every ground it is used on meets WCAG AA (4.5:1).

const here = dirname(fileURLToPath(import.meta.url));
const designMd = readFileSync(join(here, "..", "..", "..", "..", "DESIGN.md"), "utf8");
const globalsCss = readFileSync(join(here, "..", "app", "globals.css"), "utf8");

function designAppColours(): Record<string, string> {
  return Object.fromEntries(
    [...designMd.matchAll(/^ {2}(app-[a-z-]+): "(#[0-9a-f]{6})"$/gm)].map((m) => [m[1], m[2]]),
  );
}

function themeAppColours(): Record<string, string> {
  return Object.fromEntries(
    [...globalsCss.matchAll(/--color-(app-[a-z-]+):\s*(#[0-9a-f]{6});/g)].map((m) => [m[1], m[2]]),
  );
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

const GROUNDS = ["app-ground", "app-side", "app-panel", "app-raised"];
const TEXTS = [
  "app-text", "app-text-soft", "app-mute", "app-faint",
  "app-needs", "app-person", "app-proven", "app-stopped",
];

describe("Theme tokens", () => {
  it("ThemeTokens_GlobalsMirrorDesignMd_EveryAppColourEqual", () => {
    const design = designAppColours();
    expect(Object.keys(design).length).toBeGreaterThan(10);
    expect(themeAppColours()).toEqual(design);
  });

  it("ThemeTokens_EveryTextColourOnEveryGround_MeetsAa", () => {
    const c = designAppColours();
    const failing = GROUNDS.flatMap((ground) =>
      TEXTS.filter((text) => contrast(c[text], c[ground]) < 4.5).map((text) => `${text} on ${ground}`));
    expect(failing).toEqual([]);
  });

  it("ThemeTokens_BrightButton_MeetsAa", () => {
    const c = designAppColours();
    expect(contrast(c["app-on-bright"], c["app-bright"])).toBeGreaterThanOrEqual(4.5);
  });

  it("Contrast_HasTeeth_KnownPairs", () => {
    expect(contrast("#000000", "#ffffff")).toBeCloseTo(21, 0);
    expect(contrast("#777777", "#ffffff")).toBeLessThan(4.5);
  });
});
