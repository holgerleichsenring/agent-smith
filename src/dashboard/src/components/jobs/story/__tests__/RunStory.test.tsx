import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RunStory } from "../RunStory";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";

// p0344b/p0343c: the story renders REAL snapshot data — server beats, the
// persisted ledger, persisted acceptance — and renders NOTHING it would have
// to guess. The stage is BEAT-SWITCHED per the run-viewer mock: clicking a
// beat swaps the panel, and every panel binds real artifacts.

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

const BEATS = {
  ticket: "done",
  plan: "done",
  building: "active",
  verify: "pending",
  outcome: "pending",
} as const;

const LEDGER = [
  { id: "l1", activity: "Add failing test", status: "done" as const, target: "tests/LoginTests.cs" },
  { id: "l2", activity: "Fix the null check", status: "in_progress" as const, target: "src/Auth/Login.cs" },
  { id: "l3", activity: "Update docs", status: "pending" as const, target: null },
];

const TICKET_EVENT: RunEvent = {
  runId: "r1",
  type: EventType.TicketFetched,
  timestamp: "2026-07-17T10:00:01Z",
  ticketId: "T-77",
  title: "Login broken",
  description: "The login button does nothing.",
  state: "Active",
  labels: ["agent-smith"],
  attachmentCount: 2,
  source: "azdo",
};

const DECISION_EVENT: RunEvent = {
  runId: "r1",
  type: EventType.DecisionLogged,
  timestamp: "2026-07-17T10:02:00Z",
  category: "tests",
  chose: "Focus tests on public endpoints",
  over: null,
  reason: "established pattern",
};

describe("RunStory", () => {
  it("RunStory_ServerBeats_RendersStoryBarFromSnapshot", () => {
    render(<RunStory runId="r1" snapshot={snap({ beats: BEATS })} events={[]} />);
    expect(screen.getByTestId("story-bar")).toBeInTheDocument();
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "active");
  });

  it("RunStory_BeatsNull_RendersNoStoryBarAtAll", () => {
    // An old run has beats=null — no storybar, no keyword guessing.
    render(<RunStory runId="r1" snapshot={snap({ beats: null })} events={[]} />);
    expect(screen.queryByTestId("story-bar")).not.toBeInTheDocument();
    expect(screen.getByTestId("story-no-beats")).toBeInTheDocument();
  });

  it("RunStory_NoSnapshot_RendersNoStoryBar_KeepsHonestVerifyEmptyState", () => {
    render(<RunStory runId="r1" snapshot={null} events={[]} />);
    expect(screen.queryByTestId("story-bar")).not.toBeInTheDocument();
    expect(screen.getByTestId("verify-empty")).toBeInTheDocument();
  });

  it("RunViewer_BeatStage_BindsRealArtifacts", () => {
    // The default stage of an actively-building run is the Building beat —
    // the PERSISTED ledger + the decisions card from real stream events.
    render(
      <RunStory
        runId="r1"
        snapshot={snap({
          beats: BEATS,
          progressLedger: LEDGER,
          acceptance: {
            criteria: [{ text: "It works", status: "met", reason: null }],
            outcome: "verbatim",
            ratifiedBy: "holger",
          },
        })}
        events={[TICKET_EVENT, DECISION_EVENT]}
      />,
    );
    // Building (default active beat): the real ledger rows + decisions.
    expect(screen.getByTestId("beat-section-name")).toHaveTextContent("Building");
    expect(screen.getByTestId("beat-panel-building")).toBeInTheDocument();
    expect(screen.getByTestId("ledger-row-l2")).toHaveAttribute("data-status", "in_progress");
    expect(screen.getByTestId("ledger-row-l2-target")).toHaveTextContent("src/Auth/Login.cs");
    expect(screen.getByTestId("ledger-foot-caption")).toHaveTextContent("1 done · 1 now · 1 to go");
    expect(screen.getByTestId("build-notes")).toHaveTextContent("Focus tests on public endpoints");

    // Click the Ticket beat → the REAL TicketFetched body + attachment count.
    fireEvent.click(screen.getByTestId("story-beat-ticket"));
    expect(screen.getByTestId("beat-section-name")).toHaveTextContent("The ticket");
    expect(screen.getByTestId("ticket-panel-description")).toHaveTextContent(
      "The login button does nothing.",
    );
    expect(screen.getByTestId("ticket-panel-id")).toHaveTextContent("T-77");
    expect(screen.getByTestId("ticket-panel-attachments")).toHaveTextContent("2 attachments");
    // The ratified criteria render in the mock's accept-box.
    expect(screen.getByTestId("ticket-panel-acceptance")).toHaveTextContent("It works");

    // Click Verify → the persisted per-criterion dispositions.
    fireEvent.click(screen.getByTestId("story-beat-verify"));
    expect(screen.getByTestId("verify-summary")).toHaveAttribute("data-source", "acceptance");
    expect(screen.getByTestId("verify-criterion")).toHaveAttribute("data-status", "met");
  });

  it("RunStory_TicketBeat_NoEvent_HonestEmptyState", () => {
    render(
      <RunStory
        runId="r1"
        snapshot={snap({ beats: BEATS, ticketId: "T-9" })}
        events={[]}
      />,
    );
    fireEvent.click(screen.getByTestId("story-beat-ticket"));
    expect(screen.getByTestId("ticket-panel-empty")).toHaveTextContent("T-9");
  });

  it("RunStory_PreBeatsRun_StillRendersPersistedLedgerAndVerify", () => {
    render(
      <RunStory runId="r1" snapshot={snap({ progressLedger: LEDGER })} events={[]} />,
    );
    const panel = screen.getByTestId("ledger-panel");
    expect(panel).toBeInTheDocument();
    expect(screen.getByTestId("ledger-row-l1")).toHaveAttribute("data-status", "done");
    expect(screen.queryByTestId("ledger-row-l3-target")).not.toBeInTheDocument();
    expect(screen.getByTestId("verify-summary")).toBeInTheDocument();
  });

  it("Spine_NonMonotonicServerBeats_ClampedForDisplay", () => {
    // Server sent Building "done" while The plan is still "active" — impossible.
    render(
      <RunStory
        runId="r1"
        snapshot={snap({
          beats: { ticket: "done", plan: "active", building: "done", verify: "pending", outcome: "pending" },
        })}
        events={[]}
      />,
    );
    expect(screen.getByTestId("story-beat-plan")).toHaveAttribute("data-status", "active");
    // Building is clamped back to pending — it can't be done before Plan finishes.
    expect(screen.getByTestId("story-beat-building")).toHaveAttribute("data-status", "pending");
  });

  it("RunStory_LedgerFromRunStoryRecordedEvent_WhenSnapshotOmitsIt", () => {
    // The snapshot (list-row fallback) carries no progressLedger, but the run's
    // RunStoryRecorded event does — recover it instead of a false "no ledger".
    const storyEvent: RunEvent = {
      runId: "r1",
      type: EventType.RunStoryRecorded,
      timestamp: "2026-07-17T10:05:00Z",
      progressLedgerJson: JSON.stringify(LEDGER),
      acceptanceJson: null,
    };
    render(
      <RunStory
        runId="r1"
        snapshot={snap({ beats: BEATS, progressLedger: null })}
        events={[storyEvent]}
      />,
    );
    expect(screen.queryByTestId("ledger-empty")).not.toBeInTheDocument();
    expect(screen.getByTestId("ledger-panel")).toBeInTheDocument();
    expect(screen.getByTestId("ledger-row-l2")).toHaveAttribute("data-status", "in_progress");
  });

  it("RunStory_NoLedger_RendersHonestLedgerEmptyState", () => {
    render(
      <RunStory runId="r1" snapshot={snap({ beats: BEATS, progressLedger: null })} events={[]} />,
    );
    expect(screen.queryByTestId("ledger-panel")).not.toBeInTheDocument();
    expect(screen.getByTestId("ledger-empty")).toBeInTheDocument();
  });

  it("RunStory_PersistedAcceptance_PreferredOverEventFallback", () => {
    render(
      <RunStory
        runId="r1"
        snapshot={snap({
          beats: { ...BEATS, building: "done", verify: "active" },
          acceptance: {
            criteria: [{ text: "It works", status: "met", reason: null }],
            outcome: "verbatim",
            ratifiedBy: "holger",
          },
        })}
        events={[]}
      />,
    );
    // Verify is the active beat → its panel is the default stage.
    expect(screen.getByTestId("verify-summary")).toHaveAttribute("data-source", "acceptance");
    expect(screen.getByTestId("verify-criterion")).toHaveAttribute("data-status", "met");
  });
});
