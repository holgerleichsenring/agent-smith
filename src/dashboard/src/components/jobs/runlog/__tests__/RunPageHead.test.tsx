import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { metaLine, RunPageHead } from "../RunPageHead";

// 2026-11-16-6877b: the run page's header — the way back to the ticket, the title, one meta line and the run's acts.
// Carries over RunDetailHeader's cancel, delete and reserved-capacity rules.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "run-abc", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "success", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:05:27Z", finishedAt: "2026-10-13T06:11:22Z", sandboxes: 1, stepIndex: 5, stepName: null,
    totalSteps: 5, lastEventType: null, costUsd: 0.42, llmCalls: 29, ticketId: "13", ticketTitle: "Stock transfer between SKUs",
    agentName: "anthropic/sonnet", cancelRequested: false, ...over,
  };
}

const ticket = { label: "GH-13", href: "/tickets/shop/13", number: 2, of: 2 };
const head = (over: Partial<RunSnapshot> = {}, t: typeof ticket | null = ticket) =>
  render(<RunPageHead runId="run-abc" snapshot={snap(over)} ticket={t} repoNames={["server"]} onDeleted={() => {}} />);

describe("RunPageHead", () => {
  it("RunPageHead_Breadcrumb_LeadsBackToTheTicket_AndCountsTheRun", () => {
    head();
    expect(screen.getByTestId("run-crumb-ticket")).toHaveAttribute("href", "/tickets/shop/13");
    expect(screen.getByTestId("run-crumb-ticket")).toHaveTextContent("GH-13");
    expect(screen.getByTestId("run-crumb-run")).toHaveTextContent("Run 2 of 2");
    expect(screen.getByTestId("run-heading")).toHaveTextContent("Stock transfer between SKUs");
  });

  it("RunPageHead_NoTicket_LeadsBackToTheRuns_AndFallsBackToThePipeline", () => {
    head({ ticketTitle: null }, null);
    expect(screen.getByTestId("run-crumb-run")).toHaveTextContent(/^Run /);
    expect(screen.getByRole("link", { name: "Runs" })).toHaveAttribute("href", "/runs");
    expect(screen.getByTestId("run-heading")).toHaveTextContent("fix-bug");
  });

  it("RunPageHead_MetaLine_SaysTimeDurationModelReposContainersAndCost", () => {
    const parts = metaLine(snap(), ["server"]);
    expect(parts[1]).toBe("5 min 55 s");
    expect(parts.slice(2)).toEqual(["anthropic/sonnet, 29 model calls", "server", "1 container", "$0.42", "pipeline fix-bug", "run run-abc"]);
  });

  it("RunPageHead_ReservedCapacity_LabeledAsReserved_NeverMoney", () => {
    const parts = metaLine(snap({ reservedGiMinutes: 6.2 }), []);
    expect(parts).toContain("reserved 6.2 Gi·min");
  });

  // 2026-11-17-d6fbb: the side rail's facts moved into the meta line.
  it("RunPageHead_MetaLine_CarriesBudgetComputeShapePipelineAndRunId", () => {
    const parts = metaLine(snap({
      budgetCapUsd: 5, budgetTier: "medium", workShape: "judgement",
      liveCompute: { pods: [{ repo: "server", image: "node:20", mem: "2Gi", endReason: null, exitCode: null, oomKilled: null }], totalMem: "2Gi" },
    }), []);
    expect(parts).toContain("1 pod, 2Gi");
    expect(parts).toContain("$0.42 of $5.00 cap, tier medium");
    expect(parts).toContain("shape judgement");
    expect(parts).toContain("pipeline fix-bug");
    expect(parts.at(-1)).toBe("run run-abc");
    expect(parts).not.toContain("1 container");
  });

  it("RunPageHead_Cancel_ShownForRunningQueuedWaiting_NotTerminal_DeleteAlways", () => {
    const { rerender } = head({ status: "success" });
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
    expect(screen.getByTestId("delete-run-run-abc")).toBeInTheDocument();
    for (const status of ["running", "queued", "waiting_for_input"]) {
      rerender(<RunPageHead runId="run-abc" snapshot={snap({ status, finishedAt: null })} ticket={ticket} repoNames={[]} onDeleted={() => {}} />);
      expect(screen.getByTestId("cancel-run-run-abc")).toBeInTheDocument();
    }
  });

  it("RunPageHead_CancelRequested_StaysVisibleAfterTheButtonGoes", () => {
    head({ status: "failed", cancelRequested: true });
    expect(screen.getByTestId("cancel-requested-hint")).toHaveTextContent("cancel was requested");
  });

  describe("cancel confirmation", () => {
    const originalFetch = globalThis.fetch;
    beforeEach(() => { globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 202 } as Response); });
    afterEach(() => { globalThis.fetch = originalFetch; });

    it("RunPageHead_CancelClicked_ArmsFirst_ThenPosts", async () => {
      head({ status: "running", finishedAt: null });
      const cancel = screen.getByTestId("cancel-run-run-abc");
      fireEvent.click(cancel);
      expect(globalThis.fetch).not.toHaveBeenCalled();
      fireEvent.click(cancel);
      await waitFor(() => expect(globalThis.fetch).toHaveBeenCalledWith("/api/runs/run-abc/cancel", expect.objectContaining({ method: "POST" })));
      expect(screen.getByTestId("run-header-actions")).toContainElement(screen.getByTestId("delete-run-run-abc"));
    });
  });
});
