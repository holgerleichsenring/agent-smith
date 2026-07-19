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

// p0348: the pods actually spawned — the honest live-compute the rail now shows.
const LIVE_COMPUTE = {
  pods: [
    { repo: "server", image: "dotnet/sdk:8.0", mem: "1Gi", status: "created" },
    { repo: "backgroundworker", image: "dotnet/sdk:8.0", mem: "1Gi", status: "created" },
  ],
  totalMem: "2Gi",
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
    renderRail({ footprint: FOOTPRINT, liveCompute: LIVE_COMPUTE });
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("Running");
    expect(screen.getByTestId("side-rail-progress")).toHaveTextContent(/3\s*of 7 steps/);
    // p0348: the LIVE spawned pods (2), not the reservation.
    expect(screen.getByTestId("side-rail-compute-v")).toHaveTextContent("2 pods · 2Gi");
    expect(screen.getByTestId("side-rail-cost")).toHaveTextContent("$1.23");
    expect(screen.getByTestId("side-rail-elapsed")).toHaveTextContent("4m 30s");
    expect(screen.getByTestId("side-rail-elapsed")).toHaveTextContent("23 LLM");
  });

  it("RunDetail_SideRail_InProcessRun_OmitsComputeBlock", () => {
    // No footprint and no spawned sandboxes → nothing to show, no fake count.
    renderRail({ footprint: null, liveCompute: null });
    expect(screen.queryByTestId("side-rail-compute")).not.toBeInTheDocument();
    expect(screen.queryByTestId("side-rail-pods")).not.toBeInTheDocument();
  });

  it("RunDetail_SideRail_Calculating_UntilFirstSandbox", () => {
    // p0348: a run that WILL have pods (reserved footprint) but none spawned yet
    // shows "calculating…", never the reservation dressed up as live.
    renderRail({ footprint: FOOTPRINT, liveCompute: null, status: "running" });
    expect(screen.getByTestId("side-rail-compute-v")).toHaveTextContent("calculating…");
    fireEvent.click(screen.getByTestId("side-rail-compute"));
    expect(screen.getByTestId("side-rail-pods")).toHaveTextContent("Reserved at admission");
  });

  it("RunDetail_SideRail_LivePods_WinOverReservation", () => {
    renderRail({ footprint: FOOTPRINT, liveCompute: LIVE_COMPUTE });
    fireEvent.click(screen.getByTestId("side-rail-compute"));
    const pods = screen.getByTestId("side-rail-pods");
    expect(pods).toHaveTextContent("Live · the pods this run spawned");
    expect(pods).toHaveTextContent("backgroundworker");
    // The reservation-only repo ("web") is NOT shown — only what actually spawned.
    expect(pods).not.toHaveTextContent("node:20");
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
