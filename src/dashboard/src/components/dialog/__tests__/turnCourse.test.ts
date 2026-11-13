import { describe, expect, it } from "vitest";
import type { SpecDialogReviewRound } from "@/types/spec-dialog";
import { markOf, refusalsOf, waitsOnOwner } from "../turnCourse";
import type { TurnProposal } from "../turnProposal";

// 2026-11-13-b3a2: a draft refused on owner decisions only waits on the operator's answer; it is not a refusal.

const round = (specId: string, decisions: (string | null)[]): SpecDialogReviewRound => ({
  specId, round: 1, verdict: "refuse", reviewerVerdicts: ["refuse"], cuts: [], residues: [],
  findings: decisions.map((decision, i) => ({ text: `finding ${i}`, evidence: "repo/a.md:1 [A1]", decision })),
});

const proposal = (rounds: SpecDialogReviewRound[]): TurnProposal => ({ kind: "series", drafts: [], reviewRounds: rounds });

describe("turnCourse", () => {
  it("RefusedOnDecisionsOnly_WaitsMark_NotCountedAsRefusal", () => {
    const turn = proposal([round("s1", ["Is the policy approved?"]), round("s2", ["Is it approved?", null])]);

    expect(markOf(turn, "s1")).toEqual({ mark: "⏸", word: "waits on your answer" });
    expect(markOf(turn, "s2").word).toBe("refused");
    expect(refusalsOf(turn)).toBe("1 refusal open");
    expect(refusalsOf(proposal([round("s1", ["Is the policy approved?"])]))).toBeNull();
  });

  it("waitsOnOwner_NoFindings_False", () => {
    expect(waitsOnOwner(round("s1", []))).toBe(false);
  });
});
