import { describe, expect, it } from "vitest";
import type { TicketDerivation } from "@/types/tickets";
import { derivationStoryLine, foldStoryLine, runStoryLine, ticketLead } from "../storyLines";
import { attempt } from "./detailFixtures";

// 2026-11-17-8d1cb: What happened's lines from recorded fields — a run's outcome with its end kind and its own first
// line, a derivation's outcome with its time and cost, a fold of equal stops, and the ticket's first sentence.

const derivation = (over: Partial<TicketDerivation> = {}): TicketDerivation => ({
  runId: "d-1", status: "success", startedAt: "2026-09-04T09:00:00Z", finishedAt: "2026-09-04T09:04:10Z", cost: 0.42,
  outcome: "derived", specCount: 2, pullRequests: [], summary: null, ...over,
});

describe("storyLines", () => {
  it("runStoryLine_Ended_NamesOutcomeEndKindTheRunsFirstLineAndDuration", () => {
    const line = runStoryLine(attempt({ status: "failed", endKind: "preflight", summary: "Preflight failed: image\nsecond line" }));
    expect(line).toMatchObject({ who: "Run 1", outcome: "failed · preflight failed", text: "Preflight failed: image", meta: "30m 0s", tone: "bad" });
  });

  it("runStoryLine_Live_SaysItsStateWithoutAnOutcome", () => {
    const line = runStoryLine(attempt({ status: "running", finishedAt: null }));
    expect(line.outcome).toBeNull();
    expect(line.text).toBe("Run 1 is working.");
    expect(line.tone).toBe("run");
  });

  it("derivationStoryLine_SaysWhatCameOfIt_WithDurationAndCost", () => {
    expect(derivationStoryLine(derivation())).toMatchObject({ who: "Derivation", text: "Spec derivation found 2 specs.", meta: "4m 10s · $0.42", tone: "ok" });
    expect(derivationStoryLine(derivation({ outcome: "not-finished", summary: "timed out" })).tone).toBe("bad");
  });

  it("foldStoryLine_NamesTheRangeTheCauseAndTheCount", () => {
    const cause = { status: "failed", summary: "Registry not trusted", endKind: "image-untrusted" };
    const items = [3, 4, 5, 6].map((n) => ({ kind: "report" as const, attempt: attempt({ ...cause, number: n, runId: `r-${n}` }) }));
    expect(foldStoryLine(items)).toMatchObject({ who: "Runs 3–6", outcome: "failed · image not trusted", text: "Registry not trusted", meta: "4× same cause" });
    const derived = [derivation(), derivation({ runId: "d-2" })].map((d) => ({ kind: "derivation" as const, derivation: d }));
    expect(foldStoryLine(derived)).toMatchObject({ who: "Derivations", text: "2 derivations found 2 specs.", meta: "2×" });
  });

  it("ticketLead_IsThePlainFirstSentence_CutNear160", () => {
    expect(ticketLead("<p>Add a <b>transfer</b> of stock. With tests.</p>")).toBe("Add a transfer of stock.");
    expect(ticketLead("## Goal\n\nMove *stock* between SKUs")).toBe("Goal Move stock between SKUs");
    expect(ticketLead(null)).toBe("No description.");
    expect(ticketLead("x".repeat(300))).toHaveLength(160);
  });
});
