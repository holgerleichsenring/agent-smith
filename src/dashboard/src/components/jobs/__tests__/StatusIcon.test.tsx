import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { StatusIcon } from "../StatusIcon";
import type { NodeStatus } from "@/components/execution/TimingGutter";

describe("StatusIcon", () => {
  it("StatusIcon_EachStatus_RendersOutlineIconAndTone", () => {
    // p0259: soft-tint circle + outline lucide icon. cancel is its own identity
    // (slate), distinct from fail (rose), so a cancelled run never reads as failed.
    const cases: { status: NodeStatus; tone: string }[] = [
      { status: "ok", tone: "bg-emerald-50" },
      { status: "fail", tone: "bg-rose-50" },
      { status: "run", tone: "bg-amber-50" },
      { status: "wait", tone: "bg-stone-100" },
      { status: "cancel", tone: "bg-slate-100" },
      // p0320d: queued = amber clock, static (waiting for capacity, not stalled).
      { status: "queued", tone: "bg-amber-50" },
    ];
    for (const { status, tone } of cases) {
      const { unmount } = render(<StatusIcon status={status} />);
      const icon = screen.getByTestId(`status-icon-${status}`);
      expect(icon).toHaveAttribute("aria-label", status);
      expect(icon.className).toContain(tone);
      // lucide renders an inline svg; run spins, the rest are static.
      const svg = icon.querySelector("svg");
      expect(svg).not.toBeNull();
      expect(svg!.classList.contains("animate-spin")).toBe(status === "run");
      unmount();
    }
  });

  it("StatusIcon_CancelAndFail_RenderDistinctIcons", () => {
    const { container: cancel } = render(<StatusIcon status="cancel" />);
    const { container: fail } = render(<StatusIcon status="fail" />);
    const cancelIcon = cancel.querySelector("svg")?.getAttribute("class") ?? "";
    const failIcon = fail.querySelector("svg")?.getAttribute("class") ?? "";
    expect(cancelIcon).toContain("lucide-ban");
    expect(failIcon).not.toContain("lucide-ban");
  });
});
