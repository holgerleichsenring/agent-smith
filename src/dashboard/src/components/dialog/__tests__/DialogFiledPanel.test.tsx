import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogFiledPanel } from "../DialogFiledPanel";
import { DialogPane } from "../DialogPane";
import type { FiledWork, SpecDialogFilingPush } from "@/types/spec-dialog";

// 2026-09-25-c4a6: a conversation BOUND to a ticket filed nothing, so the pane has no push to
// draw from — and the ticket's runs are still its work. The read is what the pane shows, and the
// row it draws is a ticket nobody filed here: no tracker link, and a state that says so.
describe("DialogFiledPanel, for a ticket this conversation did not file", () => {
  const work: FiledWork = {
    dialogId: "d-1",
    tickets: [
      {
        reference: "DPG-1239",
        key: null,
        title: "Migrate the pipelines",
        ticketId: "DPG-1239",
        project: "alpha",
        start: {
          state: "NotFiled",
          reason: "this conversation belongs to this ticket; it did not file it",
        },
        runs: [
          {
            runId: "2026-09-25T09-00-00-0001",
            project: "alpha",
            pipeline: "phase-execution",
            status: "success",
            costUsd: 2.5,
            startedAt: "2026-09-25T09:00:00Z",
            finishedAt: null,
            pullRequests: [
              {
                repo: "api",
                status: "opened",
                url: "https://git.test/pr/1",
                reason: null,
                openedAt: "2026-09-25T10:00:00Z",
              },
            ],
            phases: [],
            pendingQuestion: null,
          },
        ],
        handback: null,
      },
    ],
  };

  it("draws the ticket and the runs that worked it", () => {
    render(<DialogFiledPanel filed={null} work={work} />);

    expect(screen.getByTestId("dialog-filed-DPG-1239").textContent).toContain(
      "Migrate the pipelines",
    );
    expect(screen.getByTestId("dialog-filed-run-2026-09-25T09-00-00-0001")).toBeInTheDocument();
    expect(screen.getByText("api")).toBeInTheDocument();
  });

  it("says the ticket was not filed here instead of claiming what it became", () => {
    render(<DialogFiledPanel filed={null} work={work} />);

    expect(screen.getByTestId("dialog-filed-start-DPG-1239").textContent).toContain(
      "not filed here",
    );
    expect(screen.queryByText("These tickets now exist.")).not.toBeInTheDocument();
  });

  it("names the ticket without a tracker link it does not have", () => {
    render(<DialogFiledPanel filed={null} work={work} />);

    // The reference is the tracker's own id, not a url: the domain ticket carries no field for
    // one, and text that reads as a link and goes nowhere is worse than text.
    expect(screen.getByText("DPG-1239").closest("a")).toBeNull();
  });

  it("still shows the filing's own tickets when there was a filing", () => {
    const filed: SpecDialogFilingPush = {
      dialogId: "d-1",
      filed: [{ reference: "https://tracker.test/1", title: "What the filing created" }],
      error: null,
      at: "2026-09-25T09:00:00Z",
    };

    render(<DialogFiledPanel filed={filed} work={work} />);

    expect(screen.getByText("These tickets now exist.")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-filed-https://tracker.test/1")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-filed-DPG-1239")).not.toBeInTheDocument();
  });

  it("offers the pane's filed tab on the read alone", () => {
    render(
      <DialogPane
        session={null}
        projects={[]}
        proposal={null}
        filed={null}
        work={work}
        focus={{ tab: "filed" }}
        onFocus={() => {}}
      />,
    );

    expect(screen.getByTestId("dialog-tab-filed")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-filed-DPG-1239")).toBeInTheDocument();
  });
});
