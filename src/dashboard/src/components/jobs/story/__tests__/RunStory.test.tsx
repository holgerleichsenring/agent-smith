import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RunStory } from "../RunStory";
import type { RunSnapshot } from "@/types/hub-events";

// p0344b: the story renders REAL snapshot data — server beats, the persisted
// ledger, persisted acceptance — and renders NOTHING it would have to guess.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "running",
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 3,
    stepName: null,
    totalSteps: 9,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

describe("RunStory", () => {
  it("RunStory_ServerBeats_RendersStoryBarFromSnapshot", () => {
    render(
      <RunStory
        snapshot={snap({
          beats: { ticket: "done", plan: "done", building: "active", verify: "pending", outcome: "pending" },
        })}
        events={[]}
      />,
    );
    expect(screen.getByTestId("story-bar")).toBeInTheDocument();
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "active");
  });

  it("RunStory_BeatsNull_RendersNoStoryBarAtAll", () => {
    // An old run has beats=null — no storybar, no keyword guessing.
    render(<RunStory snapshot={snap({ beats: null })} events={[]} />);
    expect(screen.queryByTestId("story-bar")).not.toBeInTheDocument();
  });

  it("RunStory_BeatsAbsent_RendersNoStoryBarAtAll", () => {
    render(<RunStory snapshot={snap()} events={[]} />);
    expect(screen.queryByTestId("story-bar")).not.toBeInTheDocument();
  });

  it("RunStory_NoSnapshot_RendersNoStoryBar_KeepsHonestVerifyEmptyState", () => {
    render(<RunStory snapshot={null} events={[]} />);
    expect(screen.queryByTestId("story-bar")).not.toBeInTheDocument();
    expect(screen.getByTestId("verify-empty")).toBeInTheDocument();
  });

  it("RunStory_PersistedLedger_RendersNumberedRowsWithStatusAndTarget", () => {
    render(
      <RunStory
        snapshot={snap({
          progressLedger: [
            { id: "l1", activity: "Add failing test", status: "done", target: "tests/LoginTests.cs" },
            { id: "l2", activity: "Fix the null check", status: "in_progress", target: "src/Auth/Login.cs" },
            { id: "l3", activity: "Update docs", status: "pending", target: null },
          ],
        })}
        events={[]}
      />,
    );
    const panel = screen.getByTestId("ledger-panel");
    expect(panel).toBeInTheDocument();
    expect(screen.getByTestId("ledger-row-l1")).toHaveAttribute("data-status", "done");
    expect(screen.getByTestId("ledger-row-l2")).toHaveAttribute("data-status", "in_progress");
    expect(screen.getByTestId("ledger-row-l3")).toHaveAttribute("data-status", "pending");
    // The target renders in mono; a row without a target renders none.
    expect(screen.getByTestId("ledger-row-l2-target")).toHaveTextContent("src/Auth/Login.cs");
    expect(screen.queryByTestId("ledger-row-l3-target")).not.toBeInTheDocument();
  });

  it("RunStory_NoLedger_RendersNoLedgerPanel", () => {
    render(<RunStory snapshot={snap({ progressLedger: null })} events={[]} />);
    expect(screen.queryByTestId("ledger-panel")).not.toBeInTheDocument();
  });

  it("RunStory_EmptyLedger_RendersNoLedgerPanel", () => {
    render(<RunStory snapshot={snap({ progressLedger: [] })} events={[]} />);
    expect(screen.queryByTestId("ledger-panel")).not.toBeInTheDocument();
  });

  it("RunStory_PersistedAcceptance_PreferredOverEventFallback", () => {
    render(
      <RunStory
        snapshot={snap({
          acceptance: {
            criteria: [{ text: "It works", status: "met", reason: null }],
            outcome: "verbatim",
            ratifiedBy: "holger",
          },
        })}
        events={[]}
      />,
    );
    expect(screen.getByTestId("verify-summary")).toHaveAttribute("data-source", "acceptance");
    expect(screen.getByTestId("verify-criterion")).toHaveAttribute("data-status", "met");
  });
});
