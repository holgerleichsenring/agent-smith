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

  it("RunRow_RunningStatus_ShowsStepProgressAndPulse", () => {
    render(<RunRow snapshot={base} />);
    const icon = screen.getByTestId("status-icon-run");
    expect(icon.className).toContain("animate-pulse");
    expect(screen.getByText("step 7/16")).toBeInTheDocument();
    expect(screen.getByText("running")).toBeInTheDocument();
  });

  it("RunRow_RowLinksToRunDetail", () => {
    render(<RunRow snapshot={base} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.tagName).toBe("A");
    expect(row).toHaveAttribute("href", `/jobs/${encodeURIComponent(base.runId)}`);
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
