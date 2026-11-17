import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-17-d6fbb: the side rail's remaining facts as text under the meta line.

vi.mock("@/hooks/useSandboxActivity", () => ({
  useSandboxActivity: () => ({ runId: "r1", commands: 14, lastCommand: "dotnet test" }),
}));
const { RunFacts } = await import("../RunFacts");

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:05:27Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 12,
    lastEventType: null, costUsd: 1, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false, ...over,
  };
}

describe("RunFacts", () => {
  it("RunFacts_ALiveRun_SaysProgressTimeSandboxShapeBudgetAndPods", () => {
    render(<RunFacts snapshot={snap({
      llmDurationMs: 60000, workShape: "judgement", workShapeReason: "the change needs a design call",
      budgetCapUsd: 4,
      liveCompute: { pods: [{ repo: "api", image: "node:20", mem: "2Gi", endReason: null, exitCode: null, oomKilled: null }], totalMem: "2Gi" },
    })} />);
    expect(screen.getByTestId("run-facts-line")).toHaveTextContent("step 4 of 12 · 1m model · sandbox 14 commands, last dotnet test");
    expect(screen.getByTestId("run-facts-shape")).toHaveTextContent("Shape judgement: the change needs a design call");
    expect(screen.getByTestId("run-facts-budget")).toHaveTextContent("25% of the budget spent");
    expect(screen.getByTestId("run-facts-pods")).not.toHaveAttribute("open");
    expect(screen.getByTestId("run-facts-pods")).toHaveTextContent("api");
  });

  it("RunFacts_AFinishedRunWithNothingRecorded_DrawsNothing", () => {
    const { container } = render(<RunFacts snapshot={snap({ status: "success" })} />);
    expect(container).toBeEmptyDOMElement();
  });
});
