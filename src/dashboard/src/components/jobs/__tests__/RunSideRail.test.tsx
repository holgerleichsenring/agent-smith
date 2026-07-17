import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { RunSideRail } from "../RunSideRail";
import type { RunSnapshot } from "@/types/hub-events";

// p0343c (pixel identity): the side rail is the run-viewer mock's .sidebox —
// the vertical .health metric stack, the expandable .pods detail (footprint
// only), and the two .trace-btn entry points. Dialogue renders ONLY when a
// real pending question exists; COMPUTE only when the run carries a p0336
// footprint. Everything else is honest omission.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "running",
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: "2026-07-17T10:04:30Z",
    sandboxes: 1,
    stepIndex: 3,
    stepName: null,
    totalSteps: 7,
    lastEventType: null,
    costUsd: 1.234,
    llmCalls: 23,
    ticketId: "4711",
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

const FOOTPRINT = {
  pods: [
    { repo: "server", contexts: [], image: "dotnet/sdk:8.0", cpuLimit: "1", memLimit: "1Gi" },
    { repo: "web", contexts: [], image: "node:20", cpuLimit: "1", memLimit: "1Gi" },
  ],
  totalCpuLimit: "2",
  totalMemLimit: "2Gi",
  dropped: [],
  reason: "fits",
  reserved: true,
};

const noop = () => {};

function renderRail(over: Partial<RunSnapshot> = {}, props: Partial<Parameters<typeof RunSideRail>[0]> = {}) {
  return render(
    <RunSideRail
      snapshot={snap(over)}
      hasDialogue={false}
      onOpenDialogue={noop}
      onOpenTrace={noop}
      traceSteps={snap(over).totalSteps}
      {...props}
    />,
  );
}

describe("RunSideRail", () => {
  it("RunDetail_SideRail_RendersSnapshotMetrics", () => {
    renderRail({ footprint: FOOTPRINT });
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("Running");
    expect(screen.getByTestId("side-rail-progress")).toHaveTextContent(/3\s*of 7 steps/);
    expect(screen.getByTestId("side-rail-compute-v")).toHaveTextContent("2 pods · 2Gi");
    expect(screen.getByTestId("side-rail-cost")).toHaveTextContent("$1.23");
    expect(screen.getByTestId("side-rail-elapsed")).toHaveTextContent("4m 30s");
    expect(screen.getByTestId("side-rail-elapsed")).toHaveTextContent("23 LLM");
  });

  it("RunDetail_SideRail_NoFootprint_OmitsComputeBlock", () => {
    renderRail({ footprint: null });
    expect(screen.queryByTestId("side-rail-compute")).not.toBeInTheDocument();
    expect(screen.queryByTestId("side-rail-pods")).not.toBeInTheDocument();
  });

  it("RunDetail_SideRail_ComputeClick_TogglesPodDetail", () => {
    renderRail({ footprint: FOOTPRINT });
    const pods = screen.getByTestId("side-rail-pods");
    expect(pods.className).toContain("closed");
    fireEvent.click(screen.getByTestId("side-rail-compute"));
    expect(pods.className).not.toContain("closed");
    expect(pods).toHaveTextContent("dotnet/sdk:8.0");
    expect(pods).toHaveTextContent("1Gi · 1");
    expect(pods).toHaveTextContent("Reserved at admission");
  });

  it("RunDetail_SideRail_WaitingForInput_HumanizesStatusWord", () => {
    renderRail({ status: "waiting_for_input" });
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("Needs you");
  });

  it("RunDetail_SideRail_PipelineButton_ShowsStepCountAndFires", () => {
    const open = vi.fn();
    renderRail({}, { onOpenTrace: open });
    const button = screen.getByTestId("side-rail-pipeline-jump");
    expect(button).toHaveTextContent("Full pipeline · 7 steps");
    fireEvent.click(button);
    expect(open).toHaveBeenCalledOnce();
  });

  it("RunDetail_SideRail_DialogueButton_OnlyWhenRealQuestionExists", () => {
    const open = vi.fn();
    const { unmount } = renderRail({}, { hasDialogue: false });
    expect(screen.queryByTestId("side-rail-dialogue")).not.toBeInTheDocument();
    unmount();
    renderRail({}, { hasDialogue: true, onOpenDialogue: open });
    const button = screen.getByTestId("side-rail-dialogue");
    expect(button).toHaveTextContent("Dialogue");
    expect(button).toHaveTextContent("1 open");
    fireEvent.click(button);
    expect(open).toHaveBeenCalledOnce();
  });
});
