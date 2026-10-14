import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ProposalBlock } from "../ProposalBlock";
import type { TicketConversation } from "@/types/tickets";

// 2026-10-13-8762i: the ticket page's proposal block shows each draft's review rounds, what still stands and
// what is not reviewed yet; a proposal stored before the rounds shipped shows as it did.

const proposal: NonNullable<TicketConversation["proposal"]> = {
  kind: "epic",
  drafts: [
    { specId: "2026-10-14-9000b", goal: "first slice", repos: ["shop"], done: ["done"] },
    { specId: "2026-10-14-9000c", goal: "second slice", repos: ["shop"], done: ["done"] },
  ],
  findings: [{ specId: "2026-10-14-9000c", problem: "not reviewed yet", why: "the share ran out", quote: null, evidence: null }],
  reviewRounds: [{
    specId: "2026-10-14-9000b", round: 1, verdict: "build-with-cuts", reviewerVerdicts: ["build-with-cuts", "build"],
    cuts: [{ register: "goal", quote: "first", replacement: "opening", evidence: "shop/a.cs:1 [A1]" }], findings: [], residues: [],
  }],
  notReviewed: ["2026-10-14-9000c", "series"],
};

describe("ProposalBlock review", () => {
  it("ProposalBlock_RendersRoundsStandingFindingsAndNotReviewed", () => {
    render(<ProposalBlock proposal={proposal} preview={null} />);
    const first = screen.getByTestId("proposal-draft-2026-10-14-9000b");
    expect(within(first).getByTestId("dialog-review-round")).toHaveTextContent("round 1: build-with-cuts");
    expect(within(first).getByTestId("dialog-round-cut")).toHaveTextContent("cut goal: “first” → “opening”");
    const second = screen.getByTestId("proposal-draft-2026-10-14-9000c");
    expect(within(second).getByTestId("proposal-not-reviewed-2026-10-14-9000c")).toHaveTextContent("not reviewed yet");
    expect(screen.getByTestId("proposal-not-reviewed-series")).toHaveTextContent("the series contract is not reviewed yet");
    expect(screen.getByTestId("dialog-proposal-finding")).toHaveTextContent("2026-10-14-9000c — not reviewed yet");
  });

  it("AProposalStoredBeforeTheRounds_ShowsAsItDid", () => {
    render(<ProposalBlock proposal={{ kind: "spec", drafts: [proposal.drafts[0]] }} preview={null} />);
    expect(screen.queryByTestId("dialog-proposal-rounds")).toBeNull();
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
    expect(screen.getByTestId("proposal-draft-2026-10-14-9000b")).toHaveTextContent("first slice");
  });
});
