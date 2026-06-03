import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { StatusIcon } from "../StatusIcon";
import type { NodeStatus } from "@/components/execution/TimingGutter";

describe("StatusIcon", () => {
  it("StatusIcon_EachStatus_RendersGlyphAndColor", () => {
    const cases: { status: NodeStatus; glyph: string; bg: string }[] = [
      { status: "ok", glyph: "✓", bg: "bg-emerald-600" },
      { status: "fail", glyph: "✕", bg: "bg-rose-600" },
      { status: "run", glyph: "●", bg: "bg-amber-500" },
      { status: "wait", glyph: "○", bg: "bg-stone-300" },
    ];
    for (const { status, glyph, bg } of cases) {
      const { unmount } = render(<StatusIcon status={status} />);
      const icon = screen.getByTestId(`status-icon-${status}`);
      expect(icon).toHaveTextContent(glyph);
      expect(icon.className).toContain(bg);
      if (status === "run") expect(icon.className).toContain("animate-pulse");
      unmount();
    }
  });
});
