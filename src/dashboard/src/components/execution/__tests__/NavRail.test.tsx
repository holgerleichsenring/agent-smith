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

  // p0395: the phase is a GROUP HEADER above the phase's first row, never a
  // per-row label prefix — one header per run of same-phase rows, none for
  // unspliced steps.
  it("NavRail_PhaseSteps_GroupUnderOnePhaseHeader", () => {
    const nodes = [
      node({ id: "step-0", label: "Fetch ticket" }),
      node({ id: "step-1", label: "Generate plan", phaseId: "p19106a" }),
      node({ id: "step-2", label: "Work the phase", phaseId: "p19106a" }),
      node({ id: "step-3", label: "Generate plan", phaseId: "p19106b" }),
    ];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    expect(screen.getAllByTestId("rail-phase-p19106a")).toHaveLength(1);
    expect(screen.getByTestId("rail-phase-p19106b")).toBeInTheDocument();
    expect(screen.getByTestId("rail-row-step-1-label")).toHaveTextContent(/^Generate plan$/);
    const rail = screen.getByTestId("nav-rail");
    const text = rail.textContent ?? "";
    expect(text.indexOf("p19106a")).toBeLessThan(text.indexOf("Generate plan"));
  });

  // p0398: the default view is the run's story — milestones plus gates that
  // have something to say. Internals and silent gates leave no row of their
  // own; a mechanics row stands in for them per segment.
  it("Drawer_DefaultView_ShowsMilestonesAndSpeakingGatesOnly", () => {
    const nodes = [
      node({ id: "step-0", label: "Fetch ticket", stepClass: "milestone" }),
      node({ id: "step-1", label: "Load skills", stepClass: "internal" }),
      node({ id: "step-2", label: "Hand the ticket back", stepClass: "gate", hasFinding: false }),
      node({ id: "step-3", label: "Validate phase spec", stepClass: "gate", hasFinding: true }),
      node({ id: "step-4", label: "Run master skill", stepClass: "milestone" }),
    ];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    expect(screen.getByTestId("rail-row-step-0")).toBeInTheDocument();
    expect(screen.queryByTestId("rail-row-step-1")).not.toBeInTheDocument();
    expect(screen.queryByTestId("rail-row-step-2")).not.toBeInTheDocument();
    expect(screen.getByTestId("rail-row-step-3")).toBeInTheDocument();
    expect(screen.getByTestId("rail-row-step-4")).toBeInTheDocument();
    expect(screen.getByTestId("rail-mechanics-pre:step-0")).toHaveTextContent("2 mechanics steps");
  });

  // p0398: a failing gate appears exactly when it fails — and a failed or
  // running internal is readable output too, so it surfaces regardless of class.
  it("Drawer_FailingOrRunningStep_ShowsRegardlessOfClass", () => {
    const nodes = [
      node({ id: "step-0", label: "Load skills", stepClass: "internal", status: "fail" }),
      node({ id: "step-1", label: "Validate phase spec", stepClass: "gate", hasFinding: true, status: "fail" }),
      node({ id: "step-2", label: "Write phase record", stepClass: "internal", status: "run" }),
    ];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    expect(screen.getByTestId("rail-row-step-0")).toBeInTheDocument();
    expect(screen.getByTestId("rail-row-step-1")).toBeInTheDocument();
    expect(screen.getByTestId("rail-row-step-2")).toBeInTheDocument();
    expect(screen.queryByTestId(/rail-mechanics/)).not.toBeInTheDocument();
  });

  it("Drawer_MechanicsRow_ExpandsToAllSteps", () => {
    const nodes = [
      node({ id: "step-0", label: "Fetch ticket", stepClass: "milestone" }),
      node({ id: "step-1", label: "Load skills", stepClass: "internal" }),
      node({ id: "step-2", label: "Load project context", stepClass: "internal" }),
      node({ id: "step-3", label: "Analyze codebase", stepClass: "milestone" }),
    ];
    render(<NavRail nodes={nodes} overview={overview} selection={selectionWith()} />);

    const mechanics = screen.getByTestId("rail-mechanics-pre:step-0");
    fireEvent.click(mechanics);

    // Every step of the segment is back, unchanged and in order.
    const rail = screen.getByTestId("nav-rail");
    const text = rail.textContent ?? "";
    ["Fetch ticket", "Load skills", "Load project context", "Analyze codebase"].reduce(
      (last, label) => {
        const at = text.indexOf(label);
        expect(at).toBeGreaterThan(last);
        return at;
      },
      -1,
    );
    expect(mechanics).toHaveTextContent("Hide mechanics");

    fireEvent.click(mechanics);
    expect(screen.queryByTestId("rail-row-step-1")).not.toBeInTheDocument();
  });

  // p0398: a deep link to a collapsed step must still resolve to a visible row
  // — the segment holding the selection renders expanded.
  it("Drawer_SelectedHiddenStep_ExpandsItsSegment", () => {
    const nodes = [
      node({ id: "step-0", label: "Fetch ticket", stepClass: "milestone" }),
      node({ id: "step-1", label: "Load skills", stepClass: "internal" }),
    ];
    render(
      <NavRail nodes={nodes} overview={overview} selection={selectionWith({ selected: "step-1" })} />,
    );

    expect(screen.getByTestId("rail-row-step-1")).toBeInTheDocument();
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

  // p0395b: label and meta share ONE line — meta inline, right-aligned — so a
  // typical step name reads as a single row at default rail width.
  it("RailRow_WideRail_SingleLine_MetaInline", () => {
    render(
      <RailRow
        id="step-7"
        label="Analyze codebase"
        status="ok"
        durationLabel="3m13s"
        metric="$0.42"
        isSelected={false}
        isExpanded={false}
        onSelect={() => {}}
        onToggle={() => {}}
      />,
    );

    const label = screen.getByTestId("rail-row-step-7-label");
    const meta = screen.getByTestId("rail-row-step-7-meta");
    expect(meta).toHaveTextContent("$0.42");
    expect(meta).toHaveTextContent("3m13s");
    // Same flex line: meta is the label's sibling inside one flex container,
    // pushed right — not a block under it.
    expect(meta.parentElement).toBe(label.parentElement);
    expect(meta.parentElement?.className).toContain("flex");
    expect(meta.className).toContain("ml-auto");
    expect(meta.className).toContain("whitespace-nowrap");
  });

  // p0395b: a too-narrow rail wraps the label to at most two lines instead of
  // hiding it behind an ellipsis; the clamp only engages when one line is not
  // enough, so the wide-rail default stays single-line.
  it("RailRow_NarrowRail_LabelWrapsToTwoLines", () => {
    render(
      <RailRow
        id="step-8"
        label="Work the phase: wire the fraction persistence through both drawer panes"
        status="ok"
        durationLabel="3m13s"
        isSelected={false}
        isExpanded={false}
        onSelect={() => {}}
        onToggle={() => {}}
      />,
    );

    const label = screen.getByTestId("rail-row-step-8-label");
    expect(label.className).toContain("line-clamp-2");
    expect(label.className).not.toContain("truncate");
    expect(label.className).not.toContain("whitespace-nowrap");
  });

  // A row without cost or duration (the Overview entries) renders no meta line.
  it("SidebarStepRow_WithoutMeta_RendersNoMetaLine", () => {
    render(
      <RailRow
        id="arch"
        label="Architecture"
        status="ok"
        isSelected={false}
        isExpanded={false}
        onSelect={() => {}}
        onToggle={() => {}}
      />,
    );

    expect(screen.queryByTestId("rail-row-arch-meta")).not.toBeInTheDocument();
  });
});
