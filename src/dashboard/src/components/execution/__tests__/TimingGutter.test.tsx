import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TimingGutter } from "../TimingGutter";

describe("TimingGutter", () => {
  it("TimingGutter_BarLeftWidth_DerivedFromStartDurOverTotal", () => {
    render(
      <TimingGutter startSeconds={20} durationSeconds={40} totalSeconds={100} status="ok" />,
    );
    const bar = screen.getByTestId("timing-gutter-bar");
    expect(bar.style.left).toBe("20%");
    expect(bar.style.width).toBe("40%");
  });

  it("TimingGutter_BarMinWidth_AtLeast08Percent", () => {
    render(
      <TimingGutter startSeconds={0} durationSeconds={0.01} totalSeconds={100} status="ok" />,
    );
    const bar = screen.getByTestId("timing-gutter-bar");
    expect(bar.style.width).toBe("0.8%");
  });

  it("TimingGutter_FailedStatus_RendersFailBarClass", () => {
    render(
      <TimingGutter startSeconds={0} durationSeconds={10} totalSeconds={20} status="fail" />,
    );
    expect(screen.getByTestId("timing-gutter-bar").className).toContain("rose");
  });
});
