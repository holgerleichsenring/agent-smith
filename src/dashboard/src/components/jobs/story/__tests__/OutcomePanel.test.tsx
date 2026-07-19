import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { OutcomePanel } from "../OutcomePanel";
import type { RunSnapshot } from "@/types/hub-events";

// p0350: the Outcome beat surfaces EVERY opened PR (draft or ready), not just the
// first — a multi-repo run opens several and they must all appear.

vi.mock("@/components/jobs/ResultTab", () => ({
  ResultTab: () => <div data-testid="result-tab" />,
}));

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "add-feature",
    trigger: "ticket",
    repos: ["server", "backgroundworker"],
    status: "failed",
    prUrl: null,
    summary: "keystone refused",
    startedAt: "2026-07-19T10:00:00Z",
    finishedAt: "2026-07-19T10:50:33Z",
    sandboxes: 2,
    stepIndex: 21,
    stepName: null,
    totalSteps: 22,
    lastEventType: null,
    costUsd: 25.98,
    llmCalls: 398,
    ticketId: "19106",
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

describe("OutcomePanel", () => {
  it("Outcome_ShowsAllOpenedPullRequests_DraftAndPerRepo", () => {
    render(
      <OutcomePanel
        runId="r1"
        snapshot={snap({
          pullRequests: [
            { repo: "server", url: "https://az/server/pr/1", status: "opened", isDraft: true },
            { repo: "backgroundworker", url: "https://az/bgw/pr/2", status: "opened", isDraft: true },
          ],
        })}
      />,
    );
    const links = screen.getAllByTestId("outcome-pr-link");
    expect(links).toHaveLength(2);
    expect(links[0]).toHaveAttribute("href", "https://az/server/pr/1");
    expect(links[1]).toHaveAttribute("href", "https://az/bgw/pr/2");
    // Draft on a red run, and each labelled by repo when there are several.
    expect(links[0]).toHaveTextContent("server:");
    expect(links[0]).toHaveTextContent("Draft pull request");
  });

  it("Outcome_FallsBackToSinglePrUrl_WhenListAbsent", () => {
    render(<OutcomePanel runId="r1" snapshot={snap({ status: "success", prUrl: "https://az/only/pr/9" })} />);
    const links = screen.getAllByTestId("outcome-pr-link");
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAttribute("href", "https://az/only/pr/9");
    expect(links[0]).toHaveTextContent("Pull request");
  });

  it("Outcome_NoPr_RendersNoLink", () => {
    render(<OutcomePanel runId="r1" snapshot={snap({ prUrl: null, pullRequests: [] })} />);
    expect(screen.queryByTestId("outcome-pr-link")).not.toBeInTheDocument();
  });
});
