import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { FilterRail } from "../FilterRail";
import { EventFilterProvider } from "@/lib/EventFilterContext";

const replaceMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => "/jobs/run-1",
  useSearchParams: () => new URLSearchParams(),
}));

beforeEach(() => {
  replaceMock.mockClear();
});

describe("FilterRail", () => {
  it("renders three level sections", () => {
    render(<EventFilterProvider><FilterRail /></EventFilterProvider>);
    expect(screen.getByTestId("filter-section-l1")).toBeInTheDocument();
    expect(screen.getByTestId("filter-section-l2")).toBeInTheDocument();
    expect(screen.getByTestId("filter-section-l3")).toBeInTheDocument();
  });

  it("L3 toggles start unchecked (defaults match hub gating)", () => {
    render(<EventFilterProvider><FilterRail /></EventFilterProvider>);
    expect(screen.getByTestId("filter-toggle-ToolCall")).not.toBeChecked();
    expect(screen.getByTestId("filter-toggle-ToolResult")).not.toBeChecked();
    expect(screen.getByTestId("filter-toggle-SandboxOutput")).not.toBeChecked();
  });

  it("L1 toggles start checked", () => {
    render(<EventFilterProvider><FilterRail /></EventFilterProvider>);
    expect(screen.getByTestId("filter-toggle-RunStarted")).toBeChecked();
    expect(screen.getByTestId("filter-toggle-StepStarted")).toBeChecked();
  });

  it("clicking a toggle writes the URL via router.replace", () => {
    render(<EventFilterProvider><FilterRail /></EventFilterProvider>);
    fireEvent.click(screen.getByTestId("filter-toggle-ToolCall"));
    expect(replaceMock).toHaveBeenCalled();
    const url = replaceMock.mock.calls[0][0] as string;
    expect(url).toContain("l3=ToolCall");
  });
});
