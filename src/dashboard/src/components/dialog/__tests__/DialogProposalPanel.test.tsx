import { describe, it, expect } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { DialogProposalPanel } from "../DialogProposalPanel";
import type { SpecDialogPhaseProposal, SpecDialogProposalPush } from "@/types/spec-dialog";

// 2026-10-02-3f06c: a drafted phase shows what it rests on — each fact with the evidence it cites,
// and what it states without having looked — so a finding against a citation can be read beside it.
describe("DialogProposalPanel", () => {
  const phase: SpecDialogPhaseProposal = {
    phaseId: "p9001",
    goal: "Open the seam",
    steps: ["open it"],
    tests: [],
    done: ["it is open"],
    requires: [],
    yaml: "phase: p9001",
  };

  const push = (shown: SpecDialogPhaseProposal): SpecDialogProposalPush => ({
    dialogId: "d-1",
    kind: "phase",
    bug: null,
    phase: shown,
    parent: null,
    children: [],
    at: "2026-10-02T10:00:00Z",
  });

  it("renders facts with evidence and assumptions", () => {
    render(
      <DialogProposalPanel
        proposal={push({
          ...phase,
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
    render(<DialogProposalPanel proposal={push(phase)} />);

    expect(screen.queryByTestId("dialog-proposal-facts-p9001")).toBeNull();
    expect(screen.queryByText("Assumed, not looked at")).toBeNull();
  });
});
