import { render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SpecDialogActivityStep, SpecDialogProposalPush, SpecDialogReviewRound, SpecDraftProposal } from "@/types/spec-dialog";
import { withInterrupted, type DialogEntry } from "@/hooks/useSpecDialog";
import { DialogTranscript } from "../DialogTranscript";
import { TurnMessage } from "../TurnMessage";
import { turnProposalOf } from "../turnProposal";

// 2026-11-12-92e9c: one agent turn as one unchanging message — steps, reply, one line per spec, questions as text and
// one course line — with no control inside it.

const fetchDialogActivity = vi.fn();
vi.mock("@/lib/specDialogApi", () => ({
  fetchDialogActivity: (...args: unknown[]) => fetchDialogActivity(...args),
  specDialogImageUrl: (id: number) => `/img/${id}`,
}));

const draft = (n: number): SpecDraftProposal => ({
  specId: `2026-11-12-00${n}`, goal: `goal ${n}`, steps: [], tests: [], done: [], requires: [], repos: ["server"],
});
const round = (n: number, at: number, verdict: string, finding?: string): SpecDialogReviewRound => ({
  specId: draft(n).specId, round: at, verdict, reviewerVerdicts: [verdict], cuts: [], residues: [],
  findings: finding ? [{ text: finding, evidence: "x.cs:1" }] : [],
});

const epic: SpecDialogProposalPush = {
  dialogId: "d-1", kind: "epic", bug: null, spec: null, parent: draft(0), children: [draft(1), draft(2), draft(3), draft(4)],
  at: "2026-11-12T10:00:00Z", findings: [],
  reviewRounds: [round(1, 1, "build"), round(2, 1, "build-with-cuts"), round(3, 1, "refuse", "The store is not shared."),
    round(4, 1, "refuse", "Wrong seam."), round(4, 2, "build")],
  notReviewed: null,
  questions: [{ question: "Keep CSV?", proposal: "Yes, behind a flag", specIds: [draft(2).specId] }],
  spend: { usd: 3.4, capUsd: 3, tier: "large", capHit: true },
  proposalId: "p-1",
};

const step = (seq: number, kind: string, name: string | null, detail: string | null): SpecDialogActivityStep =>
  ({ seq, kind, name, detail, timestamp: "2026-11-12T10:00:00Z" });

beforeEach(() => {
  fetchDialogActivity.mockReset();
  fetchDialogActivity.mockResolvedValue({
    count: 4, nextSeq: null, hasMore: false, firstAt: "2026-11-12T10:00:00Z", lastAt: "2026-11-12T10:12:00Z",
    steps: [step(1, "tool", "read_file", "a.cs"), step(2, "tool", "grep", "Store"), step(3, "tool", "write_spec", "s1")],
  });
});

describe("TurnMessage", () => {
  it("RendersStepsSpecsQuestionsCourse", async () => {
    render(<TurnMessage reply={<p>Four slices.</p>} proposal={turnProposalOf(epic)} runId="run-1" />);

    await waitFor(() => expect(screen.getAllByTestId("turn-step").map((line) => line.textContent))
      .toEqual(["1 read · 1 search", "write_spec s1"]));
    expect(screen.getByText("Four slices.")).toBeInTheDocument();
    expect(screen.getByTestId(`turn-spec-${draft(1).specId}`)).toHaveAttribute("data-mark", "passed");
    expect(screen.getByTestId(`turn-spec-${draft(2).specId}`)).toHaveTextContent("✂");
    expect(screen.getByTestId(`turn-spec-${draft(3).specId}`)).toHaveTextContent("✕refused: 2026-11-12-003 goal 3 — The store is not shared.");
    expect(screen.queryByTestId(`turn-spec-${draft(0).specId}`)).toBeNull();
    expect(screen.getByTestId("turn-question")).toHaveTextContent("1. Keep CSV?Proposal: Yes, behind a flag");
    const course = screen.getByTestId("turn-course");
    expect(course).toHaveTextContent("12 min · $3.40 · 2 review rounds · 2 refusals: 1 open, 1 resolved");
    expect(within(course).getByTestId("turn-course-run")).toHaveAttribute("href", "/jobs/run-1");
  });

  it("HasNoButtons", async () => {
    const { container } = render(<TurnMessage reply={<p>Done.</p>} proposal={turnProposalOf(epic)} runId="run-1" />);
    await waitFor(() => expect(screen.getAllByTestId("turn-step")).toHaveLength(2));

    expect(container.querySelectorAll("button, input, textarea, details")).toHaveLength(0);
  });

  it("BugCommentTicketList_AsLines", () => {
    const bug = { ...epic, kind: "bug", parent: null, children: [], reviewRounds: [], questions: [], spend: null,
      bug: { title: "Import drops rows", body: "long body" } };
    const comment = { ...bug, kind: "comment", bug: null, comment: "Merged into the parent." };
    const tickets = { ...bug, kind: "tickets", bug: null, tickets: [
      { key: "a", title: "First", requirement: "r", done: [], requires: [], repos: [] },
      { key: "b", title: "Second", requirement: "r", done: [], requires: ["a"], repos: [] },
    ] };
    render(<>
      <TurnMessage proposal={turnProposalOf(bug)} />
      <TurnMessage proposal={turnProposalOf(comment)} />
      <TurnMessage proposal={turnProposalOf(tickets)} />
    </>);

    expect(screen.getByTestId("turn-bug")).toHaveTextContent("Bug: Import drops rows");
    expect(screen.getByTestId("turn-bug")).not.toHaveTextContent("long body");
    expect(screen.getByTestId("turn-comment")).toHaveTextContent("Merged into the parent.");
    expect(within(screen.getByTestId("turn-ticket-lines")).getAllByRole("listitem")).toHaveLength(2);
  });

  it("CostCap_NotedOnCourseLine", () => {
    render(<TurnMessage proposal={turnProposalOf(epic)} />);

    expect(screen.getByTestId("turn-course")).toHaveTextContent("cost cap reached at $3.00");
    expect(screen.queryByTestId("turn-cap-banner")).toBeNull();
    expect(fetchDialogActivity).not.toHaveBeenCalled();
  });

  it("LiveSteps_AreShownWithoutAFetch", () => {
    const live = [{ dialogId: "d-1", kind: "model", name: null, detail: "Checking the import path", at: "2026-11-12T10:00:00Z",
      seq: 1, turnStartedAt: "2026-11-12T10:00:00Z" } as const];
    render(<TurnMessage reply={<p>Answer.</p>} runId="run-1" live={[...live]} />);

    expect(screen.getByTestId("turn-step")).toHaveTextContent("Checking the import path");
    expect(fetchDialogActivity).not.toHaveBeenCalled();
  });

  // 2026-11-11-205ff: a turn that stopped before it finished keeps its draft, between the entries at its own time.
  it("InterruptedTurn_RendersItsDraftAndSaysSo", () => {
    const placed = withInterrupted(
      [
        { key: "u", kind: "user", text: "go", at: "2026-11-12T09:00:00Z" },
        { key: "b", kind: "agent", text: "Next cut.", at: "2026-11-12T11:00:00Z", proposal: epic },
      ],
      [{ runId: "run-a", at: "2026-11-12T10:30:00Z", proposal: { ...epic, proposalId: null } }],
    );
    render(<DialogTranscript entries={placed} />);

    expect(placed.map((entry) => entry.key)).toEqual(["u", "interrupted-run-a", "b"]);
    const turns = screen.getAllByTestId("turn-message");
    expect(turns).toHaveLength(2);
    expect(within(turns[0]).getByTestId("dialog-turn-interrupted")).toHaveTextContent("stopped before it finished");
  });

  it("AnotherReplicasLiveTurn_SaysStillRunning", () => {
    const placed = withInterrupted([{ key: "u", kind: "user", text: "go", at: "2026-11-12T09:00:00Z" }],
      [{ runId: "run-a", at: "2026-11-12T10:30:00Z", proposal: { ...epic, proposalId: null }, running: true }]);
    render(<DialogTranscript entries={placed} />);

    expect(screen.getByTestId("dialog-turn-running")).toHaveTextContent("still running");
  });

  it("TwoProposingTurns_EachKeepItsOwnLines", () => {
    const first: SpecDialogProposalPush = { ...epic, proposalId: null, children: [draft(1)], questions: [] };
    const entries: DialogEntry[] = [
      { key: "a", kind: "agent", text: "First cut.", at: "2026-11-12T09:00:00Z", proposal: first },
      { key: "u", kind: "user", text: "split it", at: "2026-11-12T09:30:00Z" },
      { key: "b", kind: "agent", text: "Second cut.", at: "2026-11-12T10:00:00Z", proposal: epic },
    ];
    render(<DialogTranscript entries={entries} />);

    const turns = screen.getAllByTestId("turn-message");
    expect(within(turns[0]).getAllByTestId(/^turn-spec-2026/)).toHaveLength(1);
    expect(within(turns[1]).getAllByTestId(/^turn-spec-2026/)).toHaveLength(4);
  });
});
