import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { RunPageHead } from "../RunPageHead";

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

const ticket = { label: "GH-13", href: "/tickets/shop/13", project: "shop", number: 2, of: 2, attempt: null, newest: true };
const head = (over: Partial<RunSnapshot> = {}, t: typeof ticket | null = ticket) =>
  render(<RunPageHead runId="run-abc" snapshot={snap(over)} ticket={t} repoNames={["server"]} stepCount={5} onDeleted={() => {}} />);

describe("RunPageHead", () => {
  it("RunPageHead_Crumb_SaysRunsTheRunIdTheTicketAndRunNofM", () => {
    head();
    expect(screen.getByRole("link", { name: "Runs" })).toHaveAttribute("href", "/runs");
    expect(screen.getByTestId("run-crumb-run")).toHaveTextContent("run-abc");
    expect(screen.getByTestId("run-crumb-ticket")).toHaveAttribute("href", "/tickets/shop/13");
    expect(screen.getByTestId("run-crumb-ticket")).toHaveTextContent("ticket GH-13");
    expect(screen.getByTestId("run-crumb-count")).toHaveTextContent("Run 2 of 2");
    expect(screen.getByTestId("run-heading")).toHaveTextContent("Stock transfer between SKUs");
  });

  it("RunPageHead_NoTicket_KeepsRunsAndTheRunId_AndFallsBackToThePipeline", () => {
    head({ ticketTitle: null }, null);
    expect(screen.getByTestId("run-crumb-run")).toHaveTextContent("run-abc");
    expect(screen.queryByTestId("run-crumb-ticket")).toBeNull();
    expect(screen.getByTestId("run-heading")).toHaveTextContent("fix-bug");
  });

  it("RunPageHead_MetaLine_StateStepsElapsedCost_ThenDetails", () => {
    head({ budgetCapUsd: 5, workShape: "judgement", workShapeReason: "two repositories",
      liveCompute: { pods: [{ repo: "server", image: "node:20", mem: "2Gi", endReason: null, exitCode: null, oomKilled: null }], totalMem: "2Gi" } });
    expect(screen.getByTestId("run-meta")).toHaveTextContent("done · 5 steps · 5m 55s · $0.42 of $5.00 · 1 pod · 2Gi · shape judgement");
    expect(screen.getByTestId("run-meta-state")).toHaveAttribute("data-tone", "ok");
    const details = screen.getAllByTestId("run-detail-line").map((l) => l.textContent);
    expect(details).toContain("model anthropic/sonnet, 29 model calls");
    expect(details).toContain("pipeline fix-bug");
    expect(details).toContain("shape judgement: two repositories");
    expect(screen.getByTestId("run-detail-pod")).toHaveTextContent("server · 2Gi");
  });

  it("RunPageHead_Cancel_ShownForRunningQueuedWaiting_NotTerminal_DeleteAlways", () => {
    const { rerender } = head({ status: "success" });
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
    expect(screen.getByTestId("delete-run-run-abc")).toBeInTheDocument();
    for (const status of ["running", "queued", "waiting_for_input"]) {
      rerender(<RunPageHead runId="run-abc" snapshot={snap({ status, finishedAt: null })} ticket={ticket} repoNames={[]} stepCount={5} onDeleted={() => {}} />);
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
