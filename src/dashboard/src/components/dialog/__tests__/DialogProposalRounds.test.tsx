import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogProposalRounds } from "../DialogProposalRounds";
import { DialogDecisionBar } from "../DialogDecisionBar";
import { approvalDecision } from "../approvalDecision";
import type { SpecDialogProposalPush, SpecDialogQuestionPush, SpecDialogReviewRound } from "@/types/spec-dialog";

// 2026-10-13-8762i: the approval card shows every review round of the proposal's drafts. 2026-11-10-8e59: a spec
// not yet reviewed is marked, and Approve stays — nothing is built before a review ends clean on its exact text.

const rounds: SpecDialogReviewRound[] = [
  {
    specId: "2026-10-14-9000b", round: 1, verdict: "refuse", reviewerVerdicts: ["refuse", null],
    cuts: [{ register: "steps", quote: "buffered writer", replacement: "streaming writer", evidence: "shop/src/Writer.cs:9 [A1]" }],
    findings: [{ text: "the writer is not buffered", evidence: "shop/src/Writer.cs:9 [A1]" }],
    residues: [{ register: "done", quote: "buffered writer" }],
  },
  {
    specId: "2026-10-14-9000b", round: 2, verdict: "build", reviewerVerdicts: ["build", "build"],
    cuts: [], findings: [], residues: [],
  },
];

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-10-14T10:01:00Z", expiresAt: null,
};

function proposal(notReviewed: string[] | null): SpecDialogProposalPush {
  return {
    dialogId: "d-1", kind: "spec", bug: null, parent: null, children: [], at: "2026-10-14T10:03:00Z", findings: [],
    spec: { specId: "2026-10-14-9000b", goal: "g", steps: [], tests: [], done: [], requires: [], yaml: "" },
    reviewRounds: rounds, notReviewed,
  };
}

describe("DialogProposalRounds", () => {
  // 2026-11-19-6b0bd: no proposal is under review any more; a draft not reviewed says it will be before it is built.
  it("DialogProposalRounds_NotReviewed_SaysItWillBeBeforeItIsBuilt", () => {
    render(<DialogProposalRounds rounds={[]} notReviewed={["2026-10-14-9000b"]} />);
    expect(screen.getByTestId("dialog-not-reviewed")).toHaveTextContent("2026-10-14-9000b — not yet reviewed, will be reviewed before it is built");
  });

  it("DialogProposalRounds_RendersVerdictsCutsFindingsAndResiduePerRound", () => {
    render(<DialogProposalRounds rounds={rounds} />);
    const shown = screen.getAllByTestId("dialog-review-round");
    expect(shown).toHaveLength(2);
    expect(shown[0]).toHaveTextContent("round 1: refuse");
    expect(shown[0]).toHaveTextContent("(reviewers: refuse, not taken)");
    expect(screen.getByTestId("dialog-round-cut")).toHaveTextContent("cut steps: “buffered writer” → “streaming writer”");
    expect(screen.getByTestId("dialog-round-finding")).toHaveTextContent("the writer is not buffered");
    expect(screen.getByTestId("dialog-round-residue")).toHaveTextContent("still stands in done: “buffered writer”");
    expect(shown[1]).toHaveTextContent("round 2: build");
  });

  it("DialogProposalRounds_NoRoundsField_RendersNothing", () => {
    const { container } = render(<DialogProposalRounds rounds={undefined} notReviewed={undefined} />);
    expect(container).toBeEmptyDOMElement();
  });
});

// 2026-11-19-1b72c: the approval card left; the decision put in the composer's place carries the same rule.
describe("approval decision review rounds", () => {
  it("ApprovalDecision_NotReviewed_KeepsTheAction", () => {
    const decision = approvalDecision(question, proposal(["2026-10-14-9000b"]))!;
    expect(decision.approvable).toBe(true);
    render(<DialogDecisionBar decision={decision} onApprove={vi.fn()} onKeepChatting={vi.fn()} />);
    expect(screen.getByTestId("dialog-file")).toHaveTextContent("Create ticket");
    // 2026-11-12-92e9c: the decision counts nothing; the turn's spec lines and course line say the review.
    expect(screen.queryByTestId("dialog-review-round")).toBeNull();
  });

  it("ApprovalDecision_SeriesToken_KeepsTheAction", () => {
    expect(approvalDecision(question, proposal(["series"]))!.approvable).toBe(true);
  });

  it("AllReviewed_KeepsTheAction", () => {
    expect(approvalDecision(question, proposal(null))!.approvable).toBe(true);
  });
});
