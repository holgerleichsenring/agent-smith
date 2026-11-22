import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// 2026-11-22-7103a: BlockText reads the same wherever it renders — the rules are not scoped to the log, the face is sans,
// and every table cell (th too, which the global break-word list misses) has a border, padding and break-word.

function rule(css: string, selector: string): string {
  const line = css.split("\n").find((l) => l.startsWith(`${selector} {`));
  expect(line, selector).toBeDefined();
  return line!;
}

describe("run log css", () => {
  it("RunLogCss_MdRules_ApplyOutsideTheLogScopes_CellsBreakWordBorderAndSans", () => {
    const css = readFileSync("src/styles/run-log.css", "utf8");
    // The sans face only where a report is read; a decision's reason keeps the log's type.
    expect(rule(css, ".rl-md")).not.toContain("font-family");
    expect(rule(css, ".rl-doc-body .rl-md, .rl-report .rl-md")).toContain("font-family: var(--sans");
    const cells = rule(css, ".rl-md th, .rl-md td");
    expect(cells).toContain("overflow-wrap: break-word");
    expect(cells).toContain("border: 1px solid var(--line)");
    expect(cells).toContain("padding:");
    expect(rule(css, ".rl-md-table")).toContain("overflow-x: auto");
    expect(rule(css, ".rl-doc-body")).toContain("font-family: var(--sans");
    expect(css).not.toMatch(/:is\(\.mock-viewer, \.tk-runlog\) \.rl-(md|doc)\b/);
  });

  it("TicketBarCss_NeedsYou_IsThePersonColour", () => {
    const css = readFileSync("src/styles/ticket-thread.css", "utf8");
    expect(rule(css, ".mock-tickets .tk-bar-state.need")).toContain("color: var(--person)");
  });

  it("RunLogCss_StatusLine_ColoursOnlyItsHead", () => {
    const css = readFileSync("src/styles/run-log.css", "utf8");
    expect(rule(css, ":is(.mock-viewer, .tk-runlog) .rl-status .rl-text")).toContain("color: var(--ink-2)");
    expect(rule(css, ':is(.mock-viewer, .tk-runlog) .rl-status-head[data-tone="bad"]')).toContain("color: var(--bad)");
    expect(rule(css, ":is(.mock-viewer, .tk-runlog) .rl-log .rl-sec.quiet .rl-sec-mark")).toContain("var(--ink-3)");
  });
});
