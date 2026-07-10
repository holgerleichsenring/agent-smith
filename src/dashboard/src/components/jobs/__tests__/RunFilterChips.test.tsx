import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { RunFilterChips, countByFilter } from "../RunFilterChips";
import type { RunSnapshot } from "@/types/hub-events";

function snap(runId: string, status: string): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt: new Date().toISOString(),
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
  };
}

const runs = [
  snap("a", "running"),
  snap("b", "running"),
  snap("c", "failed"),
  snap("d", "error"),
  snap("e", "success"),
  snap("f", "queued"),
];

describe("RunFilterChips", () => {
  it("RunFilterChips_Counts_MatchStatusBuckets", () => {
    expect(countByFilter(runs, "all")).toBe(6);
    expect(countByFilter(runs, "run")).toBe(2);
    expect(countByFilter(runs, "queued")).toBe(1); // p0320d
    expect(countByFilter(runs, "fail")).toBe(2); // failed + error
    expect(countByFilter(runs, "ok")).toBe(1);

    render(<RunFilterChips runs={runs} active="all" onChange={() => {}} />);
    expect(screen.getByTestId("run-filter-all")).toHaveTextContent("6");
    expect(screen.getByTestId("run-filter-run")).toHaveTextContent("2");
    expect(screen.getByTestId("run-filter-queued")).toHaveTextContent("1");
    expect(screen.getByTestId("run-filter-fail")).toHaveTextContent("2");
    expect(screen.getByTestId("run-filter-ok")).toHaveTextContent("1");
  });

  it("RunFilterChips_ClickFailed_FiltersToFailedRuns", () => {
    const onChange = vi.fn();
    render(<RunFilterChips runs={runs} active="all" onChange={onChange} />);
    fireEvent.click(screen.getByTestId("run-filter-fail"));
    expect(onChange).toHaveBeenCalledWith("fail");
  });
});
