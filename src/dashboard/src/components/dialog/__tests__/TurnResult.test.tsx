import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SpecDialogProposalPush, SpecDraftProposal } from "@/types/spec-dialog";
import { TurnResult } from "../TurnResult";
import { turnProposalOf } from "../turnProposal";

// 2026-11-10-d854b: one reduced result per proposing turn — summary, activity line, cap banner, questions and one
// closed row per spec — on a fixture epic of ten specs, three questions and a capped review.
// 2026-11-12-92e9c: the ticket page's result; the conversation's turn is TurnMessage (its own test), so the cases of
// held answers, Take/Undo, the approve bar and the run's paged steps left with it.

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

describe("TurnResult", () => {
  it("the fixture shows one activity line, the cap banner, three questions and ten closed rows", () => {
    render(<TurnResult proposal={turnProposalOf(epic)} />);

    expect(screen.getAllByTestId("turn-activity-line")).toHaveLength(1);
    expect(screen.getByTestId("turn-activity-line")).toHaveTextContent("3 review rounds");
    expect(screen.getByTestId("turn-activity-cost")).toHaveTextContent("$5.72");
    expect(screen.getByTestId("turn-cap-banner")).toHaveTextContent("Cost cap reached at $5.00 — 7 specs not reviewed");
    expect(screen.getAllByTestId("turn-question")).toHaveLength(3);
    expect(screen.getAllByTestId("turn-row-toggle")).toHaveLength(10);
    expect(screen.queryByTestId("turn-row-open")).toBeNull();
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
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

  // 2026-11-12-01b4: with review off nothing reviewed the specs, so no row says reviewed clean and nothing counts clean.
  it("TurnResult with no review shows no pill and no clean count", () => {
    const unreviewed = { ...epic, findings: [], reviewRounds: [], notReviewed: null, spend: null };
    render(<TurnResult proposal={turnProposalOf(unreviewed)} />);

    expect(screen.getAllByTestId(/^turn-row-2026/)).toHaveLength(10);
    expect(screen.queryByTestId("turn-row-status")).toBeNull();
    expect(screen.getByTestId("turn-specs")).not.toHaveTextContent("clean");
  });

  it("TurnResult says reviewed clean only for the spec whose last round answered build", () => {
    const rounds = [
      { specId: draft(1).specId, round: 1, verdict: "build", reviewerVerdicts: ["build"], cuts: [], findings: [], residues: [] },
      { specId: draft(2).specId, round: 1, verdict: "build", reviewerVerdicts: ["build"], cuts: [], findings: [], residues: [] },
      { specId: draft(2).specId, round: 2, verdict: "refuse", reviewerVerdicts: ["refuse"], cuts: [], findings: [], residues: [] },
    ];
    render(<TurnResult proposal={turnProposalOf({ ...epic, findings: [], reviewRounds: rounds, notReviewed: null, spend: null })} />);

    expect(screen.getAllByTestId("turn-row-status").map((pill) => pill.textContent)).toEqual(["reviewed clean", "findings"]);
    expect(screen.getByTestId("turn-specs")).toHaveTextContent("1 clean · 1 with findings");
  });

  it("TurnResult shows the cap banner only when Spend.capHit", () => {
    const { rerender } = render(<TurnResult proposal={turnProposalOf({ ...epic, spend: { ...epic.spend!, capHit: false } })} />);
    expect(screen.queryByTestId("turn-cap-banner")).toBeNull();
    rerender(<TurnResult proposal={turnProposalOf(epic)} />);
    expect(screen.getByTestId("turn-cap-banner")).toBeInTheDocument();
  });

  // 2026-11-12-92e9c: the questions are text on the ticket page too; nothing holds or sends an answer.
  it("TurnQuestions_AreNumberedText_WithoutControls", () => {
    render(<TurnResult proposal={turnProposalOf(epic)} />);

    const questions = screen.getAllByTestId("turn-question");
    expect(questions[2]).toHaveTextContent("3. Which event contract?");
    expect(questions[2]).toHaveTextContent("Proposal: Transactional outbox");
    expect(within(screen.getByTestId("turn-questions")).queryAllByRole("button")).toHaveLength(0);
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
});
