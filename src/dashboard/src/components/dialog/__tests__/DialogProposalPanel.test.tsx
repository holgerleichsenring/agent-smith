import { describe, it, expect } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { DialogProposalPanel } from "../DialogProposalPanel";
import type { SpecDraftProposal, SpecDialogProposalPush } from "@/types/spec-dialog";

// 2026-10-02-3f06c: a drafted spec shows what it rests on — each fact with the evidence it cites,
// and what it states without having looked — so a finding against a citation can be read beside it.
describe("DialogProposalPanel", () => {
  const spec: SpecDraftProposal = {
    specId: "p9001",
    goal: "Open the seam",
    steps: ["open it"],
    tests: [],
    done: ["it is open"],
    requires: [],
    yaml: "spec: p9001",
  };

  const push = (shown: SpecDraftProposal): SpecDialogProposalPush => ({
    dialogId: "d-1",
    kind: "spec",
    bug: null,
    spec: shown,
    parent: null,
    children: [],
    at: "2026-10-02T10:00:00Z",
  });

  it("renders facts with evidence and assumptions", () => {
    render(
      <DialogProposalPanel
        proposal={push({
          ...spec,
          facts: [{ claim: "the seam exists", evidence: "repo-a/src/Seam.cs:3-9" }],
          assumptions: ["nobody calls it yet"],
        })}
      />,
    );

    const facts = screen.getByTestId("dialog-proposal-facts-p9001");
    expect(within(facts).getByText("the seam exists")).toBeInTheDocument();
    expect(within(facts).getByText("repo-a/src/Seam.cs:3-9")).toBeInTheDocument();
    expect(screen.getByText("nobody calls it yet")).toBeInTheDocument();
  });

  it("leaves both sections out for a proposal stored before they were carried", () => {
    render(<DialogProposalPanel proposal={push(spec)} />);

    expect(screen.queryByTestId("dialog-proposal-facts-p9001")).toBeNull();
    expect(screen.queryByText("Assumed, not looked at")).toBeNull();
  });

  // 2026-10-13-8762t: the strip is drawn beside the spec or slice whose versions the conversation holds.
  it("renders the strip only for the spec or slice that has versions, never for an epic's parent", () => {
    const slice = (specId: string): SpecDraftProposal => ({ ...spec, specId });
    const epic: SpecDialogProposalPush = { ...push(spec), kind: "epic", spec: null, parent: slice("parent"), children: [slice("c-1"), slice("c-2")] };
    const mocks = [{ specId: "c-2", version: 1, name: "c-2-mock-v1", at: "2026-10-13T10:00:00Z", hasShot: false }];

    render(<DialogProposalPanel proposal={epic} mocks={mocks} ratifiedMocks={null} sessionId="s-1" filed={false} />);

    expect(screen.getByTestId("dialog-mock-versions-c-2")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-mock-versions-c-1")).toBeNull();
    expect(screen.queryByTestId("dialog-mock-versions-parent")).toBeNull();
  });
  // 2026-10-28-7a69c: a proposed comment is shown as the text that would be posted.
  it("RendersAProposedComment", () => {
    render(
      <DialogProposalPanel
        proposal={{ ...push(spec), kind: "comment", spec: null, comment: "Needs a decision first.\nAsk the platform team." }}
      />,
    );

    expect(screen.getByTestId("dialog-proposal-comment").textContent).toContain("Ask the platform team.");
  });
});
