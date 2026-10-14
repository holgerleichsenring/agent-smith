import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogProposalRounds } from "../DialogProposalRounds";
import { DialogQuestionCard } from "../DialogQuestionCard";
import type { SpecDialogProposalPush, SpecDialogQuestionPush, SpecDialogReviewRound } from "@/types/spec-dialog";

// 2026-10-13-8762i: the approval card shows every review round of the proposal's drafts and drops its Approve
// control while a spec (or the series contract) is not reviewed yet.

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

describe("DialogQuestionCard review rounds", () => {
  it("DialogQuestionCard_NotReviewed_HasNoApproveControl", () => {
    render(<DialogQuestionCard question={question} proposal={proposal(["2026-10-14-9000b"])} onAnswer={vi.fn()} />);
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-not-reviewed")).toHaveTextContent("2026-10-14-9000b — not reviewed yet");
    expect(screen.getAllByTestId("dialog-review-round")).toHaveLength(2);
  });

  it("DialogQuestionCard_SeriesToken_SaysTheSeriesContractIsNotReviewedYet", () => {
    render(<DialogQuestionCard question={question} proposal={proposal(["series"])} onAnswer={vi.fn()} />);
    expect(screen.getByTestId("dialog-not-reviewed")).toHaveTextContent(/^the series contract is not reviewed yet$/);
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
  });

  it("AllReviewed_KeepsTheApproveControl", () => {
    render(<DialogQuestionCard question={question} proposal={proposal(null)} onAnswer={vi.fn()} />);
    expect(screen.getByTestId("dialog-answer-approve")).toBeInTheDocument();
  });
});
