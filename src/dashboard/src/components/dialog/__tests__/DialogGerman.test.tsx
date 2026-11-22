import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { translator } from "@/lib/i18n/translate";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";
import { DialogDecisionBar } from "../DialogDecisionBar";
import { DialogQuestionCard } from "../DialogQuestionCard";
import { approvalDecision } from "../approvalDecision";

// 2026-11-22-5485a: Work it out in German — the decision that replaces the composer and a question card read from the
// dictionary; what the design partner wrote is shown as it came.

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-11-22T10:01:00Z", expiresAt: null,
};
const tickets = ["a", "b", "c"].map((key) => ({ key, title: key, requirement: "r", done: [], requires: [], repos: [] }));
const list = {
  dialogId: "d-1", kind: "tickets", bug: null, spec: null, parent: null, children: [], at: "2026-11-22T10:03:00Z",
  findings: [], tickets,
} as unknown as SpecDialogProposalPush;

describe("Work it out in German", () => {
  it("DialogDecisionBar_German_OffersTicketsAndKeepChatting", () => {
    resetLanguage("de");
    const decision = approvalDecision(translator("de"), question, list)!;
    render(<DialogDecisionBar decision={decision} onApprove={vi.fn()} onKeepChatting={vi.fn()} />);
    expect(screen.getByTestId("dialog-file")).toHaveTextContent("3 Tickets anlegen");
    expect(screen.getByTestId("dialog-keep-chatting")).toHaveTextContent("Weiter chatten");
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(
      "Diese 3 Tickets anlegen? 3 warten auf nichts; die übrigen warten, bis gemergt ist, was sie voraussetzen.",
    );
  });

  it("DialogQuestionCard_German_WordsTheCardButNotTheQuestion", () => {
    resetLanguage("de");
    render(
      <DialogQuestionCard
        question={{ ...question, kind: "confirmation", text: "Shall the export run nightly?" }}
        onAnswer={vi.fn()}
      />,
    );
    expect(screen.getByText("wartet auf dich")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-answer-yes")).toHaveTextContent("Ja");
    expect(screen.getByTestId("dialog-answer-no")).toHaveTextContent("Nein");
    expect(screen.getByText("Shall the export run nightly?")).toBeInTheDocument();
  });
});
