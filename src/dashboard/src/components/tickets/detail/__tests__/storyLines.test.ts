import { describe, expect, it } from "vitest";
import type { TicketDerivation } from "@/types/tickets";
import { derivationStoryLine, foldStoryLine, runStoryLine, ticketLead } from "../storyLines";
import { attemptStatusInput, runStatus } from "@/components/jobs/runlog/runStatusLine";
import { attempt } from "./detailFixtures";

// 2026-11-17-8d1cb: What happened's lines from recorded fields — a run's outcome with its end kind and its own first
// line, a derivation's outcome with its time and cost, a fold of equal stops, and the ticket's first sentence.

const derivation = (over: Partial<TicketDerivation> = {}): TicketDerivation => ({
  runId: "d-1", status: "success", startedAt: "2026-09-04T09:00:00Z", finishedAt: "2026-09-04T09:04:10Z", cost: 0.42,
  outcome: "derived", specCount: 2, pullRequests: [], summary: null, ...over,
});

describe("storyLines", () => {
  // 2026-11-22-7103a: a run's line is its status line — the head in its colour, then the clauses and the next step.
  it("runStoryLine_Ended_IsTheStatusHeadAndDuration", () => {
    const line = runStoryLine(attempt({ status: "failed", summary: "Preflight failed: image\nsecond line" }));
    expect(line).toMatchObject({ who: "Run 1", outcome: "Failed", text: "Preflight failed: image.", meta: "30m 0s", tone: "bad" });
  });

  it("runStoryLine_Live_SaysItIsWorking", () => {
    const line = runStoryLine(attempt({ status: "running", finishedAt: null }));
    expect(line).toMatchObject({ outcome: "Working", text: "", tone: "run" });
  });

  it("RunStoryLine_Shortfall_OutcomeIsTheStatusHeadAndToneBad", () => {
    const a = attempt({ status: "shortfall", executedSpecs: [
      { specId: "s1", ordinal: 1, title: "one", status: "done", verdict: null },
      { specId: "s2", ordinal: 2, title: "two", status: "failed", verdict: "The agent ended without writing a report. More." }] });
    const line = runStoryLine(a, runStatus(attemptStatusInput(a, "Review the pull request", true)));
    expect(line).toMatchObject({ outcome: "1 of 2 specs delivered", tone: "bad",
      text: "s2: The agent ended without writing a report. → Review the pull request; Retry unchanged gives s2 another attempt." });
  });

  it("derivationStoryLine_SaysWhatCameOfIt_WithDurationAndCost", () => {
    expect(derivationStoryLine(derivation())).toMatchObject({ who: "Derivation", text: "Spec derivation found 2 specs.", meta: "4m 10s · $0.42", tone: "ok" });
    expect(derivationStoryLine(derivation({ outcome: "not-finished", summary: "timed out" })).tone).toBe("bad");
  });

  it("foldStoryLine_NamesTheRangeTheCauseAndTheCount", () => {
    const cause = { status: "failed", summary: "Registry not trusted" };
    const items = [3, 4, 5, 6].map((n) => ({ kind: "report" as const, attempt: attempt({ ...cause, number: n, runId: `r-${n}` }) }));
    expect(foldStoryLine(items)).toMatchObject({ who: "Runs 3–6", outcome: "Failed: Registry not trusted", meta: "4× same cause" });
    const derived = [derivation(), derivation({ runId: "d-2" })].map((d) => ({ kind: "derivation" as const, derivation: d }));
    expect(foldStoryLine(derived)).toMatchObject({ who: "Derivations", text: "2 derivations found 2 specs.", meta: "2×" });
  });

  it("ticketLead_IsThePlainFirstSentence_CutNear160", () => {
    expect(ticketLead("<p>Add a <b>transfer</b> of stock. With tests.</p>")).toBe("Add a transfer of stock.");
    expect(ticketLead("## Goal\n\nMove *stock* between SKUs")).toBe("Goal Move stock between SKUs");
    expect(ticketLead(null)).toBe("No description.");
    expect(ticketLead("x".repeat(300))).toHaveLength(160);
  });

  it("FoldStoryLine_ReadsTheLastRunsStatusLine", () => {
    const items = [1, 2].map((n) => ({ kind: "report" as const, attempt: attempt({ number: n, runId: `r-${n}`, status: "failed", summary: `Cause ${n}` }) }));
    expect(foldStoryLine(items)).toMatchObject({ who: "Runs 1–2", outcome: "Failed: Cause 2" });
    const status = runStatus({ ...attemptStatusInput(items[1].attempt), next: "Reply below" });
    expect(foldStoryLine(items, status)).toMatchObject({ outcome: "Failed: Cause 2", text: "→ Reply below." });
  });
});
