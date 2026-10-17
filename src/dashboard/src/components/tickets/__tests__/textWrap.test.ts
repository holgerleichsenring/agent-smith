import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// 2026-10-16-7ed3c: one base rule breaks a long token in any paragraph; the ticket page's text panes wrap anywhere
// and may shrink in their track.

describe("text wrap", () => {
  it("textWrap_PaneRule_CoversTheTicketTextPanes", () => {
    const css = readFileSync("src/styles/mock-parity.css", "utf8");
    const rule = css.split("\n").find((line) => line.includes("overflow-wrap: anywhere; min-width: 0;") && line.includes(":is("));
    expect(rule).toBeDefined();
    for (const pane of [".why", ".doc", ".msg .mb", ".spdoc .sb", ".sd-b > div", ".ts-say", ".crit .ci > span"]) {
      expect(rule).toContain(pane);
    }
  });

  it("textWrap_BaseRule_BreaksLongWordsInParagraphs", () => {
    const css = readFileSync("src/app/globals.css", "utf8");
    expect(css).toMatch(/p, li, dd, blockquote, td, h1, h2, h3, h4, h5, h6 \{\s*overflow-wrap: break-word;/);
  });
});
