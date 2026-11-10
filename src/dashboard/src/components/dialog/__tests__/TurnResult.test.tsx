import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SpecDialogActivityPage, SpecDialogProposalPush, SpecDraftProposal } from "@/types/spec-dialog";
import type { DialogEntry } from "@/hooks/useSpecDialog";
import { DialogTranscript } from "../DialogTranscript";
import { TurnResult } from "../TurnResult";
import { turnProposalOf } from "../turnProposal";

// 2026-11-10-d854b: one reduced result per proposing turn — summary, activity line, cap banner, questions and one
// closed row per spec — on a fixture epic of ten specs, three questions, a capped review and 1,225 steps.

const fetchDialogActivity = vi.fn();
vi.mock("@/lib/specDialogApi", () => ({
  fetchDialogActivity: (...args: unknown[]) => fetchDialogActivity(...args),
  specDialogImageUrl: (id: number) => `/img/${id}`,
}));

const draft = (n: number): SpecDraftProposal => ({
  specId: `2026-11-10-00${String(n).padStart(2, "0")}`, goal: `goal ${n}`, steps: [], tests: [], done: [`done ${n}`],
  requires: [], yaml: `spec: ${n}`, facts: [{ claim: `claim ${n}`, evidence: `file${n}.cs:1` }], repos: [n % 2 ? "server" : "client"],
});

const epic: SpecDialogProposalPush = {
  dialogId: "d-1", kind: "epic", bug: null, spec: null, parent: draft(0),
  children: Array.from({ length: 10 }, (_, i) => draft(i + 1)), at: "2026-11-10T19:00:00Z",
  findings: [{ specId: draft(5).specId, problem: "missing", why: "certificate check", quote: null, evidence: null }],
  reviewRounds: [{ specId: draft(10).specId, round: 3, verdict: "build", reviewerVerdicts: ["build"], cuts: [], findings: [], residues: [] }],
  notReviewed: Array.from({ length: 8 }, (_, i) => draft(i + 1)).filter((d) => d.specId !== draft(5).specId).map((d) => d.specId),
  questions: [
    { question: "Who may a recipient forward to?", proposal: "Any active user", specIds: [draft(1).specId] },
    { question: "Do notices expire?", proposal: "No expiry", specIds: [] },
    { question: "Which event contract?", proposal: "Transactional outbox", specIds: [draft(1).specId, draft(4).specId] },
  ],
  spend: { usd: 5.72, capUsd: 5, tier: "large", capHit: true },
  proposalId: "p-2",
};

const page = (count: number, from: number, size: number, nextSeq: number | null): SpecDialogActivityPage => ({
  count, nextSeq, hasMore: nextSeq !== null, firstAt: "2026-11-10T19:00:00Z", lastAt: "2026-11-10T19:20:00Z", subAgentCalls: 4,
  steps: Array.from({ length: size }, (_, i) => ({ kind: "tool", name: "read_file", detail: `f${from + i}.cs`, seq: from + i, timestamp: "2026-11-10T19:01:00Z" })),
});

describe("TurnResult", () => {
  beforeEach(() => {
    fetchDialogActivity.mockReset();
    fetchDialogActivity.mockImplementation((_run: string, after: number | null) =>
      Promise.resolve(after == null ? page(1225, 1, 200, 200) : page(1225, after + 1, 25, null)));
  });

  it("the fixture shows the summary, one activity line, the cap banner, three questions and ten closed rows", async () => {
    render(<TurnResult proposal={turnProposalOf(epic)} summary={<p>Epic: ten specs.</p>} runId="run-1" />);

    expect(screen.getByText("Epic: ten specs.")).toBeInTheDocument();
    expect(screen.getAllByTestId("turn-activity-line")).toHaveLength(1);
    expect(screen.getByTestId("turn-cap-banner")).toHaveTextContent("Cost cap reached at $5.00 — 7 specs not reviewed");
    expect(screen.getAllByTestId("turn-question")).toHaveLength(3);
    expect(screen.getAllByTestId("turn-row-toggle")).toHaveLength(10);
    expect(screen.queryByTestId("turn-row-open")).toBeNull();
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
    await waitFor(() => expect(screen.getByTestId("turn-activity-line")).toHaveTextContent("1,225 steps"));
  });

  it("TurnResult shows one row per spec in build order with the right pill", () => {
    render(<TurnResult proposal={turnProposalOf(epic)} />);

    const rows = screen.getAllByTestId(/^turn-row-2026/);
    expect(rows.map((row) => row.getAttribute("data-testid"))).toEqual(epic.children.map((c) => `turn-row-${c.specId}`));
    expect(screen.queryByTestId(`turn-row-${epic.parent!.specId}`)).toBeNull();
    expect(within(rows[4]).getByTestId("turn-row-status")).toHaveTextContent("findings");
    expect(within(rows[0]).getByTestId("turn-row-status")).toHaveTextContent("not reviewed");
    expect(within(rows[9]).getByTestId("turn-row-status")).toHaveTextContent("reviewed clean");
  });

  it("TurnResult's activity line shows duration, steps and cost and pages the steps", async () => {
    render(<TurnResult proposal={turnProposalOf(epic)} runId="run-1" />);
    const line = screen.getByTestId("turn-activity-line");
    await waitFor(() => expect(line).toHaveTextContent("Worked 20 min · 1,225 steps · 4 sub-agent calls · 3 review rounds"));
    expect(screen.getByTestId("turn-activity-cost")).toHaveTextContent("$5.72");

    fireEvent.click(line);
    expect(screen.getAllByTestId("turn-activity-step")).toHaveLength(200);
    fireEvent.click(screen.getByTestId("turn-activity-more"));

    await waitFor(() => expect(screen.getAllByTestId("turn-activity-step")).toHaveLength(225));
    expect(fetchDialogActivity).toHaveBeenLastCalledWith("run-1", 200, 200);
  });

  it("TurnResult shows the cap banner only when Spend.capHit", () => {
    const { rerender } = render(<TurnResult proposal={turnProposalOf({ ...epic, spend: { ...epic.spend!, capHit: false } })} />);
    expect(screen.queryByTestId("turn-cap-banner")).toBeNull();
    rerender(<TurnResult proposal={turnProposalOf(epic)} />);
    expect(screen.getByTestId("turn-cap-banner")).toBeInTheDocument();
  });

  it("Take proposal sends the question and the proposal; Answer hands the question to the composer", () => {
    const onTake = vi.fn();
    const onAnswer = vi.fn();
    render(<TurnResult proposal={turnProposalOf(epic)} onTake={onTake} onAnswer={onAnswer} />);

    fireEvent.click(screen.getAllByTestId("turn-question-take")[1]);
    fireEvent.click(screen.getAllByTestId("turn-question-answer")[0]);

    expect(onTake).toHaveBeenCalledWith("Do notices expire? — No expiry");
    expect(onAnswer).toHaveBeenCalledWith("Who may a recipient forward to?");
  });

  it("A row opens to its findings, done criteria and links", () => {
    render(<TurnResult proposal={turnProposalOf(epic)} />);
    const row = screen.getByTestId(`turn-row-${draft(5).specId}`);

    fireEvent.click(within(row).getByTestId("turn-row-toggle"));
    expect(within(row).getByTestId("turn-row-findings")).toHaveTextContent("missing: certificate check");
    expect(within(row).getByTestId("turn-row-done")).toHaveTextContent("done 5");
    fireEvent.click(within(row).getByTestId("turn-row-link-yaml"));
    expect(within(row).getByTestId("turn-row-yaml")).toHaveTextContent("spec: 5");
    fireEvent.click(within(row).getByTestId("turn-row-link-facts"));
    expect(within(row).getByTestId("turn-row-facts")).toHaveTextContent("claim 5 — file5.cs:1");
  });

  it("Two proposing turns each render their own TurnResult; only the latest has the approve bar", () => {
    const first: SpecDialogProposalPush = { ...epic, proposalId: null, children: [draft(1)], questions: [] };
    const entries: DialogEntry[] = [
      { key: "a", kind: "agent", text: "First cut.", at: "2026-11-10T18:00:00Z", proposal: first, runId: "run-1" },
      { key: "u", kind: "user", text: "split it", at: "2026-11-10T18:30:00Z" },
      { key: "b", kind: "agent", text: "Second cut.", at: "2026-11-10T19:00:00Z", proposal: epic, runId: "run-2" },
    ];
    render(<DialogTranscript entries={entries} onInspect={vi.fn()}
      approval={{ proposalId: "p-2", node: <div data-testid="approve-bar">Approve</div> }} />);

    const results = screen.getAllByTestId("turn-result");
    expect(results).toHaveLength(2);
    expect(within(results[0]).queryByTestId("approve-bar")).toBeNull();
    expect(within(results[1]).getByTestId("approve-bar")).toBeInTheDocument();
    expect(within(results[0]).getAllByTestId("turn-row-toggle")).toHaveLength(1);
  });
});
