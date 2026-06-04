import { render, screen, within } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { Chip } from "../Chip";

describe("Chip", () => {
  it("Chip_SelectedAndCount_RenderConsistentlyAcrossUsages", () => {
    // Two different usages of the one primitive must render identical selected
    // styling and count markup — the whole point of consolidating the three
    // former chip implementations.
    render(<Chip testId="usage-a" label="All" selected count={5} onClick={() => {}} />);
    render(<Chip testId="usage-b" label="Findings" selected count={5} onClick={() => {}} />);

    const a = screen.getByTestId("usage-a");
    const b = screen.getByTestId("usage-b");

    expect(a.getAttribute("data-active")).toBe("true");
    expect(b.getAttribute("data-active")).toBe("true");
    expect(a.className).toBe(b.className);

    const countA = within(a).getByTestId("chip-count");
    const countB = within(b).getByTestId("chip-count");
    expect(countA.className).toBe(countB.className);
    expect(a).toHaveTextContent("5");
  });

  it("Chip_Unselected_DropsSelectedState", () => {
    render(<Chip testId="off" label="x" selected={false} onClick={() => {}} />);
    const chip = screen.getByTestId("off");
    expect(chip.getAttribute("data-active")).toBe("false");
    expect(chip.getAttribute("aria-pressed")).toBe("false");
  });
});
