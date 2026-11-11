import { describe, expect, it } from "vitest";
import { approvalReviewLine } from "../approvalReviewLine";
import type { SpecDialogReviewRound } from "@/types/spec-dialog";

function round(specId: string, n: number, verdict: string): SpecDialogReviewRound {
  return { specId, round: n, verdict, reviewerVerdicts: [verdict], cuts: [], findings: [], residues: [] };
}

describe("approvalReviewLine", () => {
  it("approvalReviewLine_CountsLastVerdictPerSpecFindingsAndUnreviewed", () => {
    const line = approvalReviewLine({
      reviewRounds: [
        round("a", 1, "refuse"), round("a", 2, "build"),
        round("b", 1, "refuse"),
        round("c", 1, "build-with-cuts"),
        round("series", 1, "refuse"),
      ],
      findings: [
        { specId: "b", problem: "refuse", why: "x", quote: null, evidence: null },
        { specId: "c", problem: "build-with-cuts", why: "y", quote: null, evidence: null },
      ],
      notReviewed: ["d"],
    });
    expect(line).toBe("Review of 3 specs: 1 refused, 1 need cuts, 1 clean. 2 findings, each under its spec above. 1 not reviewed yet.");
  });

  it("approvalReviewLine_NothingToSay_ReturnsNull", () => {
    expect(approvalReviewLine({})).toBeNull();
    expect(approvalReviewLine({ reviewRounds: [], findings: [], notReviewed: null })).toBeNull();
  });
});
