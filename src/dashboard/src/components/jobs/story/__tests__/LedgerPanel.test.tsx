import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { LedgerPanel } from "../LedgerPanel";

// 2026-11-17-d6fbd: a done row's tick sits on the green fill. Stroked white it read at about 1.6:1; it takes the
// on-fill colour from its box instead, the same token every filled mark on the page uses.

describe("LedgerPanel", () => {
  it("LedgerPanel_DoneTick_StrokesTheBoxColourWhichIsTheOnFillToken", () => {
    render(<LedgerPanel entries={[{ id: "a", activity: "Add the endpoint", status: "done", target: null }]} />);
    const tick = screen.getByTestId("ledger-row-a").querySelector("svg path");
    expect(tick).toHaveAttribute("stroke", "currentColor");
    const css = readFileSync(join(process.cwd(), "src/styles/mock-parity.css"), "utf8");
    expect(css).toMatch(/\.mock-viewer \.li\.done \.box \{[^}]*color: var\(--on-fill\)/);
  });
});
