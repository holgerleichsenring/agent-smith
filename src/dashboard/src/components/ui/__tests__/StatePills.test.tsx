import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { StatePills, type StatePill } from "../StatePills";

// 2026-11-16-0440b: a list's states as filter pills.

type Key = "all" | "needs" | "done";

const PILLS: StatePill<Key>[] = [
  { key: "all", label: "All", href: "/x", count: 5 },
  { key: "needs", label: "Needs you", href: "/x?bucket=needs", count: 2, tone: "needs" },
  { key: "done", label: "Done", href: "/x?bucket=done" },
];

function renderPills(onSelect = vi.fn(), selected: Key = "needs") {
  render(<StatePills label="State" pills={PILLS} selected={selected} onSelect={onSelect} testId="p" />);
  return onSelect;
}

describe("StatePills", () => {
  it("StatePills_SelectedPill_IsTheCurrentPage", () => {
    renderPills();
    expect(screen.getByTestId("p-needs")).toHaveAttribute("aria-current", "page");
    expect(screen.getByTestId("p-needs").className).toContain("on");
    expect(screen.getByTestId("p-all")).not.toHaveAttribute("aria-current");
  });

  it("StatePills_Counts_ShowWithTheirToneAndAnUnknownCountShowsNone", () => {
    renderPills();
    expect(screen.getByTestId("p-needs-count")).toHaveTextContent("2");
    expect(screen.getByTestId("p-needs-count").className).toContain("needs");
    expect(screen.getByTestId("p-all-count")).toHaveTextContent("5");
    expect(screen.queryByTestId("p-done-count")).not.toBeInTheDocument();
  });

  it("StatePills_EachPill_IsALinkToItsFilteredAddress", () => {
    renderPills();
    expect(screen.getByTestId("p-done")).toHaveAttribute("href", "/x?bucket=done");
  });

  it("StatePills_PlainClick_SelectsInPlace", () => {
    const onSelect = renderPills();
    fireEvent.click(screen.getByTestId("p-done"));
    expect(onSelect).toHaveBeenCalledWith("done");
  });

  it("StatePills_ModifiedClick_LeavesItToTheBrowser", () => {
    const onSelect = renderPills();
    fireEvent.click(screen.getByTestId("p-done"), { metaKey: true });
    expect(onSelect).not.toHaveBeenCalled();
  });
});
