import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RunRow } from "../RunRow";
import type { RunSnapshot } from "@/types/hub-events";

const base: RunSnapshot = {
  runId: "2026-06-03T10-00-00-abcd",
  pipeline: "fix-bug",
  trigger: "ticket",
  repos: ["server", "web"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: new Date(Date.now() - 64_000).toISOString(),
  finishedAt: null,
  sandboxes: 2,
  stepIndex: 7,
  stepName: "AnalyzeCode",
  totalSteps: 16,
  lastEventType: "StepStarted",
  costUsd: 0,
  llmCalls: 0,
  ticketId: "18803",
  ticketTitle: "AuthController coverage",
  agentName: "azure_openai",
  cancelRequested: false,
};

describe("RunRow", () => {
  it("RunRow_FailedStatus_RendersFailIconAndProgressLabel", () => {
    render(
      <RunRow
        snapshot={{
          ...base,
          status: "failed",
          stepIndex: 11,
          totalSteps: 18,
          finishedAt: new Date().toISOString(),
        }}
      />,
    );
    expect(screen.getByTestId("status-icon-fail")).toBeInTheDocument();
    expect(screen.getByText("failed · 11/18")).toBeInTheDocument();
  });

  it("RunRow_RunningStatus_ShowsStepProgressAndSpinner", () => {
    render(<RunRow snapshot={base} />);
    // p0259: the running indicator now spins (Loader2) instead of pulsing the
    // filled container — part of the lighter status-icon restyle.
    const icon = screen.getByTestId("status-icon-run");
    expect(icon.querySelector("svg")?.classList.contains("animate-spin")).toBe(true);
    expect(screen.getByText("step 7/16")).toBeInTheDocument();
    expect(screen.getByText("running")).toBeInTheDocument();
  });

  it("RunRow_RowLinksToRunDetail", () => {
    render(<RunRow snapshot={base} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.tagName).toBe("A");
    expect(row).toHaveAttribute("href", `/jobs/${encodeURIComponent(base.runId)}`);
  });

  it("RunRow_QueuedStatus_ShowsPositionAndReason", () => {
    // p0320d: a queued run shows its FIFO place and WHY it waits — never the
    // misleading stepIndex/totalSteps fill that reads like a stalled run.
    render(
      <RunRow
        snapshot={{
          ...base,
          status: "queued",
          summary: "waiting for sandbox capacity",
          queuePosition: 3,
        }}
      />,
    );
    expect(screen.getByTestId("status-icon-queued")).toBeInTheDocument();
    expect(screen.getByText("queued · #3")).toBeInTheDocument();
    expect(screen.getByText("waiting for sandbox capacity")).toBeInTheDocument();
    expect(screen.queryByText("step 7/16")).not.toBeInTheDocument();
    expect(screen.queryByText("7/16")).not.toBeInTheDocument();
  });

  it("RunRow_QueuedWithoutPosition_ShowsPlainQueuedLabel", () => {
    render(<RunRow snapshot={{ ...base, status: "queued", queuePosition: null }} />);
    expect(screen.getByText("queued")).toBeInTheDocument();
  });

  it("RunRow_CancelRequested_BadgeVisible_AnyStatus", () => {
    // p0330: the durable cancelRequested flag shows on the live list row —
    // "cancelling…" while the run is not yet terminal, a muted hint when it
    // ended before the cancel was enforced.
    const { unmount } = render(
      <RunRow snapshot={{ ...base, status: "queued", cancelRequested: true }} />,
    );
    expect(screen.getByTestId("cancel-requested-badge")).toHaveTextContent("cancelling…");
    unmount();
    render(
      <RunRow
        snapshot={{
          ...base,
          status: "success",
          finishedAt: new Date().toISOString(),
          cancelRequested: true,
        }}
      />,
    );
    expect(screen.getByTestId("cancel-requested-hint")).toHaveTextContent("cancel was requested");
  });

  it("RunRow_Cancelled_ShowsNoCancelRequestedBadge", () => {
    render(<RunRow snapshot={{ ...base, status: "cancelled", cancelRequested: true }} />);
    expect(screen.queryByTestId("cancel-requested-badge")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cancel-requested-hint")).not.toBeInTheDocument();
  });

  it("RunRow_NoReposNoTitle_RendersHonestlyWithoutSynthesis", () => {
    render(
      <RunRow
        snapshot={{ ...base, repos: [], ticketId: null, ticketTitle: null }}
      />,
    );
    expect(screen.queryByText("AuthController coverage")).not.toBeInTheDocument();
    expect(screen.getByText("no repos")).toBeInTheDocument();
  });
});
