import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ProposalBlock } from "../ProposalBlock";
import type { TicketApprovalPreview, TicketConversation } from "@/types/tickets";

// 2026-10-13-8762i: the ticket page's proposal block shows each draft's review rounds, what still stands and
// what is not reviewed yet; a proposal stored before the rounds shipped shows as it did.
// 2026-11-10-d854b: it renders the conversation's TurnResult — one closed row per spec — with this page's own parts.

const proposal: NonNullable<TicketConversation["proposal"]> = {
  kind: "spec",
  drafts: [
    { specId: "2026-10-14-9000b", goal: "first slice", repos: ["shop"], done: ["done"], yaml: "spec: 2026-10-14-9000b" },
  ],
  findings: [{ specId: "2026-10-14-9000b", problem: "missing", why: "the refund case", quote: null, evidence: null }],
  reviewRounds: [{
    specId: "2026-10-14-9000b", round: 1, verdict: "build-with-cuts", reviewerVerdicts: ["build-with-cuts", "build"],
    cuts: [{ register: "goal", quote: "first", replacement: "opening", evidence: "shop/a.cs:1 [A1]" }], findings: [], residues: [],
  }],
  notReviewed: ["series"],
};

const open = (specId: string) => {
  const row = screen.getByTestId(`turn-row-${specId}`);
  fireEvent.click(within(row).getByTestId("turn-row-toggle"));
  return row;
};

describe("ProposalBlock review", () => {
  it("ProposalBlock_RendersRoundsStandingFindingsAndNotReviewed", () => {
    render(<ProposalBlock proposal={proposal} preview={null} />);
    const row = open("2026-10-14-9000b");
    expect(within(row).getByTestId("turn-row-status")).toHaveTextContent("findings");
    expect(within(row).getByTestId("turn-row-findings")).toHaveTextContent("missing: the refund case");
    fireEvent.click(within(row).getByTestId("turn-row-link-rounds"));
    expect(within(row).getByTestId("dialog-review-round")).toHaveTextContent("round 1: build-with-cuts");
    expect(within(row).getByTestId("dialog-round-cut")).toHaveTextContent("cut goal: “first” → “opening”");
    expect(screen.getByTestId("turn-not-reviewed-series"))
      .toHaveTextContent("the series contract — not yet reviewed, will be reviewed before it is built");
  });

  it("AProposalStoredBeforeTheRounds_ShowsAsItDid", () => {
    render(<ProposalBlock proposal={{ kind: "spec", drafts: [proposal.drafts[0]] }} preview={null} />);
    expect(screen.queryByTestId("dialog-proposal-rounds")).toBeNull();
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
    expect(screen.getByTestId("turn-row-2026-10-14-9000b")).toHaveTextContent("first slice");
  });

  // 2026-10-13-8762t
  it("ProposalBlock names the ratified mock version", () => {
    const drafts = [{ ...proposal.drafts[0], ratifiedMock: 3 }, { ...proposal.drafts[0], specId: "2026-10-14-9000c" }];
    render(<ProposalBlock proposal={{ kind: "spec", drafts }} preview={null} />);
    expect(screen.getByTestId("proposal-ratified-mock-2026-10-14-9000b")).toHaveTextContent("mock v3 ratified");
    expect(screen.queryByTestId("proposal-ratified-mock-2026-10-14-9000c")).toBeNull();
  });

  // 2026-11-10-d854b
  it("ProposalBlock renders TurnResult with dispositions and ratified mocks", () => {
    const preview = {
      effect: "continues", specs: [
        { specId: "2026-10-14-9000b", goal: "first slice", disposition: "recut", note: null },
        { specId: "2026-10-14-9000a", goal: "already built", disposition: "kept", note: null },
      ],
    } as unknown as TicketApprovalPreview;
    const drafts = [{ ...proposal.drafts[0], ratifiedMock: 2 }];
    render(<ProposalBlock proposal={{ ...proposal, drafts, questions: [{ question: "Keep CSV?", proposal: "Keep it", specIds: [] }] }}
      preview={preview} />);

    expect(screen.getByTestId("turn-result")).toBeInTheDocument();
    expect(screen.getByTestId("proposal-draft-2026-10-14-9000b")).toHaveTextContent("recut");
    expect(screen.getByTestId("proposal-ratified-mock-2026-10-14-9000b")).toHaveTextContent("mock v2 ratified");
    expect(screen.getByTestId("proposal-spec-2026-10-14-9000a")).toHaveTextContent("already built · kept");
    expect(screen.getByTestId("turn-question")).toHaveTextContent("Keep CSV?");
    expect(screen.queryByTestId("turn-question-take")).toBeNull();
  });
});
