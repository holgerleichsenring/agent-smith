import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { NavRail, type OverviewRailItem } from "../NavRail";
import { RailRow } from "../RailRow";
import type { ExecutionNodeProps } from "../ExecutionNode";
import type { RailSelection } from "@/hooks/useRailSelection";

function node(over: Partial<ExecutionNodeProps> & { id: string; label: string }): ExecutionNodeProps {
  return {
    status: "ok", depth: 0, startSeconds: 0, durationSeconds: 1, totalSeconds: 10,
    durationLabel: "1.0s", ...over,
  };
}

function selectionWith(over: Partial<RailSelection> = {}): RailSelection {
  return {
    selected: "", expanded: new Set<string>(),
    select: vi.fn(), toggle: vi.fn(), ...over,
  };
}

const overview: OverviewRailItem[] = [
  { id: "arch", label: "Architecture", status: "ok" },
  { id: "result", label: "Result", status: "fail" },
];

describe("NavRail", () => {
  it("NavRail_RendersExecutionAndOverviewSections_InOrder", () => {
    const nodes = [node({ id: "step-0", label: "Load catalog" })];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    const rail = screen.getByTestId("nav-rail");
    const text = rail.textContent ?? "";
    expect(text.indexOf("Execution")).toBeGreaterThanOrEqual(0);
    expect(text.indexOf("Execution")).toBeLessThan(text.indexOf("Overview"));
    expect(screen.getByTestId("rail-row-arch")).toBeInTheDocument();
    expect(screen.getByTestId("rail-row-result")).toBeInTheDocument();
  });

  it("NavRail_ClickRow_SelectsAndExpandsParent", () => {
    const select = vi.fn();
    const nodes = [
      node({
        id: "step-9", label: "Analyze codebase",
        children: [node({ id: "sub-x", label: "sub-agent: x", depth: 1 })],
      }),
    ];
    // parent expanded so the child row is rendered
    render(
      <NavRail
        nodes={nodes}
        overview={overview}
        selection={selectionWith({ expanded: new Set(["step-9"]), select })}
      />,
    );

    fireEvent.click(screen.getByTestId("rail-row-sub-x"));

    expect(select).toHaveBeenCalledWith("sub-x", "step-9");
  });

  it("NavRail_CollapsedParent_HidesChildren", () => {
    const nodes = [
      node({
        id: "step-9", label: "Analyze codebase",
        children: [node({ id: "sub-x", label: "sub-agent: x", depth: 1 })],
      }),
    ];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    expect(screen.queryByTestId("rail-row-sub-x")).not.toBeInTheDocument();
  });
});

describe("RailRow", () => {
  it("RailRow_StatusDotAndDuration_MatchNode", () => {
    render(
      <RailRow
        id="step-14"
        label="Run tests"
        status="fail"
        durationLabel="3m13s"
        isSelected={false}
        isExpanded={false}
        onSelect={() => {}}
        onToggle={() => {}}
      />,
    );

    expect(screen.getByTestId("rail-dot-fail")).toBeInTheDocument();
    expect(screen.getByText("3m13s")).toBeInTheDocument();
  });

  it("RailRow_ChevronClick_TogglesWithoutSelecting", () => {
    const onSelect = vi.fn();
    const onToggle = vi.fn();
    render(
      <RailRow
        id="step-9"
        label="Analyze codebase"
        status="ok"
        durationLabel="2m19s"
        hasChildren
        isSelected={false}
        isExpanded={false}
        onSelect={onSelect}
        onToggle={onToggle}
      />,
    );

    fireEvent.click(screen.getByTestId("rail-chevron-step-9"));

    expect(onToggle).toHaveBeenCalledTimes(1);
    expect(onSelect).not.toHaveBeenCalled();
  });
});
