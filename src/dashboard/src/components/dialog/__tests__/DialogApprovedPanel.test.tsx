import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogApprovedPanel } from "../DialogApprovedPanel";
import type { ApprovedSetView } from "@/types/spec-dialog";

// 2026-09-25-8e51d: the pane shows what a person APPROVED for this ticket — labelled as the
// approval it is, because the branch is what a run reads and the two can differ once a ticket
// has been worked.
describe("DialogApprovedPanel", () => {
  const approved: ApprovedSetView = {
    key: "jira-dpg-1239",
    tracker: "sample-jira",
    approvedAt: "2026-09-20T10:00:00Z",
    approvedBy: "someone",
    approvedInConversation: "s-42",
    repositories: ["repo-a"],
    phases: [
      {
        phaseId: "p0001a",
        goal: "Make the thing work",
        steps: [],
        tests: [],
        done: ["the thing works"],
        requires: [],
        yaml: "phase: p0001a",
      },
    ],
  };

  it("names whose approval it shows and warns it is not what a run is working", () => {
    render(<DialogApprovedPanel approved={approved} />);

    const line = screen.getByTestId("dialog-approved-approval").textContent ?? "";
    expect(line).toContain("someone");
    expect(line).toContain("not necessarily what a run is working");
  });

  it("draws every approved phase with its done criteria", () => {
    render(<DialogApprovedPanel approved={approved} />);

    expect(screen.getByTestId("dialog-approved-phase-p0001a").textContent).toContain(
      "Make the thing work",
    );
    expect(screen.getByText("the thing works")).toBeInTheDocument();
  });
});
