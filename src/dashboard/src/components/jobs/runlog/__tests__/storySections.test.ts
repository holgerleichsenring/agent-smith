import { describe, expect, it } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { outcomeSection, planSection, ticketSection } from "../storySections";
import { statusNote } from "../RunStatusBox";

// 2026-11-17-d6fbb: the log's Ticket, Spec and plan, and Outcome sections, and the status box's added note.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "success", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:05:27Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 12,
    lastEventType: null, costUsd: 1, llmCalls: 0, ticketId: "GH-13", ticketTitle: "Stock transfer", agentName: null,
    cancelRequested: false, ...over,
  };
}

describe("storySections", () => {
  it("ticketSection_NamesTheTicket", () => {
    expect(ticketSection(snap())).toMatchObject({ id: "ticket", title: "Ticket", state: "done", summary: "GH-13 · Stock transfer" });
    expect(ticketSection(null).state).toBe("pending");
  });

  it("planSection_CountsTheLedger", () => {
    expect(planSection([{ id: "a", activity: "x", status: "done", target: null }, { id: "b", activity: "y", status: "in_progress", target: null }]))
      .toMatchObject({ title: "Spec and plan", state: "running", summary: "1 of 2 plan steps done" });
    expect(planSection(null)).toMatchObject({ state: "pending", summary: "no plan recorded yet" });
  });

  it("outcomeSection_FollowsTheStatusAndThePullRequests", () => {
    expect(outcomeSection(snap({ prUrl: "https://example.test/pr/1" }))).toMatchObject({ state: "done", summary: "1 pull request" });
    expect(outcomeSection(snap({ status: "failed" }))).toMatchObject({ state: "bad", summary: "stopped, no pull request opened" });
    expect(outcomeSection(snap({ status: "running" }))).toMatchObject({ state: "pending", summary: "not finished yet" });
  });

  it("statusNote_WaitingHoldsCompute_FinishedWithoutAPullRequestSaysSo", () => {
    expect(statusNote(snap({ status: "waiting_for_input" }), true, false)).toBe("Reserved compute is held; no tokens are spent while it waits.");
    expect(statusNote(snap(), false, false)).toBe("No pull request opened; the Outcome section says why.");
    expect(statusNote(snap({ prUrl: "https://example.test/pr/1" }), false, false)).toBeNull();
    expect(statusNote(snap({ status: "running" }), false, false)).toBeNull();
  });
});
