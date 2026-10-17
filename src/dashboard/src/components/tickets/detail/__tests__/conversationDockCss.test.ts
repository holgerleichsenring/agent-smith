import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// 2026-10-17-e730a: the composer's dock sticks to the bottom of the scroll box; only its inner layer scrolls.

function rule(css: string, selector: string): string {
  const line = css.split("\n").find((l) => l.startsWith(`${selector} {`));
  expect(line, selector).toBeDefined();
  return line!;
}

describe("conversation dock css", () => {
  it("ConversationDockCss_IsStickyAtTheBottom_WithTheSafeAreaInset_AndOnlyTheInnerLayerScrolls", () => {
    const css = readFileSync("src/styles/mock-parity.css", "utf8");
    const dock = rule(css, ".mock-tickets .conv-dock");
    expect(dock).toContain("position:sticky");
    expect(dock).toContain("bottom:0");
    expect(dock).toContain("env(safe-area-inset-bottom, 0px)");
    expect(dock).not.toContain("overflow");
    expect(rule(css, ".mock-tickets .conv-dock-body")).toContain("overflow-y:auto");
  });
});
