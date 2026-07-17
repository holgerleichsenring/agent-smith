import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";

const fetchRunMock = vi.fn();
vi.mock("@/lib/runsApi", () => ({
  fetchRun: (...args: unknown[]) => fetchRunMock(...args),
}));

import { useRunDetailSnapshot } from "../useRunDetailSnapshot";

function snap(runId: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "success",
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: "2026-07-17T10:05:00Z",
    sandboxes: 1,
    stepIndex: 5,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 1,
    llmCalls: 3,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

// Probe component: renders what the hook returns, so tests observe through the
// DOM (same act() semantics as real usage) instead of renderHook internals.
function Probe({ list }: { list: RunSnapshot }) {
  const merged = useRunDetailSnapshot(list.runId, list);
  return (
    <div data-testid="probe">
      {merged?.progressLedger ? `ledger:${merged.progressLedger.length}` : "no-ledger"}
    </div>
  );
}

describe("useRunDetailSnapshot", () => {
  beforeEach(() => fetchRunMock.mockReset());

  it("DetailFetched_DetailOnlyFields_WinOverListRow", async () => {
    fetchRunMock.mockResolvedValue(
      snap("r1", {
        progressLedger: [{ id: "1", activity: "Do it", status: "done", target: null }],
      }),
    );
    render(<Probe list={snap("r1")} />);
    await waitFor(() => expect(screen.getByTestId("probe")).toHaveTextContent("ledger:1"));
  });

  it("DetailUnavailable_ListRowKeepsRendering", async () => {
    // fetchRun resolves null on 404 — the hook must keep serving the list row.
    // (The network-error catch takes the identical fallback path.)
    fetchRunMock.mockResolvedValue(null);
    render(<Probe list={snap("r1")} />);
    await waitFor(() => expect(fetchRunMock).toHaveBeenCalled());
    expect(screen.getByTestId("probe")).toHaveTextContent("no-ledger");
  });
});
