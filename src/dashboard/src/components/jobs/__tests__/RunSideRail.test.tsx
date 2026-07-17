import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { RunSideRail } from "../RunSideRail";
import type { RunSnapshot } from "@/types/hub-events";

// p0343b: the run-detail side rail renders SNAPSHOT data only. COMPUTE appears
// only when the run carries a p0336 footprint; there is deliberately NO
// Dialogue button (no thread-read endpoint exists — honest omission).

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

describe("RunSideRail", () => {
  it("RunDetail_SideRail_RendersSnapshotMetrics", () => {
    render(
      <RunSideRail
        snapshot={snap({
          footprint: {
            pods: [
              { repo: "server", contexts: [], image: "img", cpuLimit: "1", memLimit: "1Gi" },
              { repo: "web", contexts: [], image: "img", cpuLimit: "1", memLimit: "1Gi" },
            ],
            totalCpuLimit: "2",
            totalMemLimit: "2Gi",
            dropped: [],
            reason: "fits",
            reserved: true,
          },
        })}
        onJumpToPipeline={() => {}}
      />,
    );
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("running");
    expect(screen.getByTestId("side-rail-progress")).toHaveTextContent("3 of 7");
    expect(screen.getByTestId("side-rail-compute")).toHaveTextContent("2 pods · 2Gi");
    expect(screen.getByTestId("side-rail-cost")).toHaveTextContent("$1.23");
    expect(screen.getByTestId("side-rail-elapsed")).toHaveTextContent("4m 30s · 23 LLM");
  });

  it("RunDetail_SideRail_NoFootprint_OmitsComputeBlock", () => {
    render(<RunSideRail snapshot={snap({ footprint: null })} onJumpToPipeline={() => {}} />);
    expect(screen.queryByTestId("side-rail-compute")).not.toBeInTheDocument();
  });

  it("RunDetail_SideRail_WaitingForInput_HumanizesStatusWord", () => {
    render(<RunSideRail snapshot={snap({ status: "waiting_for_input" })} onJumpToPipeline={() => {}} />);
    expect(screen.getByTestId("side-rail-state")).toHaveTextContent("waiting for input");
  });

  it("RunDetail_SideRail_PipelineJump_ShowsStepCountAndFires", () => {
    const jump = vi.fn();
    render(<RunSideRail snapshot={snap()} onJumpToPipeline={jump} />);
    const button = screen.getByTestId("side-rail-pipeline-jump");
    expect(button).toHaveTextContent("Full pipeline · 7 steps");
    fireEvent.click(button);
    expect(jump).toHaveBeenCalledOnce();
  });

  it("RunDetail_SideRail_NoDialogueButton_HonestOmission", () => {
    render(<RunSideRail snapshot={snap()} onJumpToPipeline={() => {}} />);
    expect(screen.queryByText(/dialogue/i)).not.toBeInTheDocument();
  });
});
