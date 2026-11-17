import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";

// 2026-11-17-d6fbd: the tool panels were drawn for a dark-on-light theme — bg-stone-950/900 under light text — and the
// dark remap turns those backgrounds near-white. They draw from the app-* set now, which means the same thing in
// every theme.

const FILES = ["RunToolsPanel.tsx", "ToolPairCard.tsx", "ToolCallRow.tsx", "ToolResultRow.tsx"];
const RAW = /\b(?:bg|text|border)-(?:stone|emerald|rose|amber)-\d{2,3}\b/g;

describe("tool panel colours", () => {
  it("ToolPanels_UseAppTokens_NoRawPaletteClass", () => {
    for (const file of FILES) {
      const source = readFileSync(join(process.cwd(), "src/components/jobs", file), "utf8");
      expect(source.match(RAW) ?? [], file).toEqual([]);
    }
  });
});
