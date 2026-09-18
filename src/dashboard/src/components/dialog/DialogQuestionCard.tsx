"use client";

import { DialogProposalFindings } from "@/components/dialog/DialogProposalFindings";
import { Markdown } from "@/components/ui/Markdown";
import type {
  SpecDialogChoice,
  SpecDialogDecision,
  SpecDialogProposalPush,
  SpecDialogQuestionPush,
} from "@/types/spec-dialog";

// 2026-09-15-cb3e: the affordance a typed question needs. The controls come from the
// question itself — a Choice question carries its choices, and the APPROVAL that files
// tickets carries none at all: every adapter builds the approve/reject pair from the kind,
// which is why the kind travels with the push.
//
// An answer goes back as an ordinary message through the same endpoint the composer uses,
// so a click and a typed reply take one path — and anything else the operator types is the
// edit note the flow already routes.
//
// 2026-09-17-c7aed: the approval is the decision, so it gets a surface of its own — what
// will be filed, counted from the proposal, above the confirmation text as sent. Nothing is
// said about the order the slices run in: that depends on the project, not on the proposal.
//
// 2026-09-17-042ek: and the confirmation the server sends a PAGE now carries no text at all,
// because this card already says all of it — the counted summary, 042ed's findings, and the
// two buttons that are the approve/reject sentence. What the card cannot say by itself is
// what would be filed when the proposal did not reach it, and the two ways that happens are
// not the same thing, so they do not get the same sentence. With NO proposal the draft is
// nowhere on this page — the shown transcript strips every draft block and the seed drops the
// agent entry that leaves empty — so the honest advice is to reject and ask again. With a
// proposal this build cannot count, the findings below still come from it and the operator
// can read it in the pane; only the one-line count is missing.
//
// 2026-09-17-042ef: the card is the Projects page's panel card, and the accent line around it
// is this page's own modifier — it says the conversation is waiting on a person, which is a
// state the studio's cards never have. An expired card keeps the plain card line, because
// nothing is waiting any more. The two controls are the studio's buttons.

// The server pushes nothing when a wait expires — the confirmer simply stops waiting and
// clears its pending entry. So the deadline is what the card has, and a click past it is
// not an answer: it posts the literal word as a message and buys a whole design turn.
function hasExpired(question: SpecDialogQuestionPush): boolean {
  return question.expiresAt !== null && Date.parse(question.expiresAt) <= Date.now();
}

interface Control {
  label: string;
  answer: string;
  primary: boolean;
  /** 2026-09-17-042el: the approval buttons are a decision, shown as one rather than echoed. */
  decision?: SpecDialogDecision;
}

export function DialogQuestionCard({
  question,
  proposal,
  onAnswer,
}: {
  question: SpecDialogQuestionPush;
  proposal: SpecDialogProposalPush | null;
  onAnswer: (answer: string, decision?: SpecDialogDecision) => void;
}) {
  const expired = hasExpired(question);
  const controls = expired ? [] : controlsFor(question);
  const decision = question.kind === "approval" && !expired;
  const summary = decision && proposal ? summaryOf(proposal) : null;
  // Only where the server said nothing AND the card has nothing of its own: a chat-shaped
  // confirmation that still carries its text keeps that text, and shows no second line.
  const unsummarised = decision && !summary && question.text.trim().length === 0;
  return (
    <div
      data-testid="dialog-question"
      data-kind={question.kind}
      className={expired ? "ecard inert d-body text-body" : "ecard inert waiting d-body"}
    >
      <div className={expired ? "fl mb-1" : "fl on mb-1"}>
        {expired ? "no longer waiting — nothing was filed" : "waiting on you"}
      </div>
      {summary && (
        <p data-testid="dialog-approval-summary" className="dsh-body font-semibold text-ink">
          {summary}
        </p>
      )}
      {unsummarised && (
        <p data-testid="dialog-approval-unsummarised" className="dsh-body font-semibold text-ink">
          {proposal
            ? "A proposal is waiting for your decision; this page cannot count what it would file. The Proposal tab has it in full."
            : "Nothing was saved about this proposal, so this page cannot show it. Reject it and ask for a fresh one."}
        </p>
      )}
      {decision && proposal && <DialogProposalFindings findings={proposal.findings} />}
      {question.text.trim().length > 0 && <Markdown>{question.text}</Markdown>}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {controls.map((control) => (
          <button
            key={control.answer}
            type="button"
            data-testid={`dialog-answer-${control.answer}`}
            onClick={() => onAnswer(control.answer, control.decision)}
            className={control.primary ? "btn primary" : "btn"}
          >
            {control.label}
          </button>
        ))}
        {/* 2026-09-17-042ek: an EXPIRED card promised a revision it cannot deliver — the
            confirmer has stopped waiting and the timeout cleared the stored proposal, so the
            next message buys a whole design turn instead. That was survivable while the server
            text was still on the card; with the text now empty it was the only sentence left. */}
        <span className="ec-sub min-w-44 flex-1">
          {expired
            ? "The wait is over — anything you write below starts a new turn."
            : decision
              ? "Anything you write below is a note, and the proposal is revised with it."
              : "Anything else you write below is a note — the proposal is revised with it."}
        </span>
      </div>
    </div>
  );
}

function controlsFor(question: SpecDialogQuestionPush): Control[] {
  if (question.choices.length > 0) return question.choices.map(asControl);
  if (question.kind === "approval")
    return [
      { label: "Approve & file", answer: "approve", primary: true, decision: "approved" },
      { label: "Reject", answer: "reject", primary: false, decision: "rejected" },
    ];
  if (question.kind === "confirmation")
    return [
      { label: "Yes", answer: "yes", primary: true },
      { label: "No", answer: "no", primary: false },
    ];
  // Free text and info questions are answered by writing, which the composer already does.
  return [];
}

function asControl(choice: SpecDialogChoice): Control {
  return { label: choice.label, answer: choice.label, primary: false };
}

/** What the proposal says would be filed, and nothing it does not say. */
function summaryOf(proposal: SpecDialogProposalPush): string | null {
  if (proposal.bug) return "File this bug? One fix-bug ticket.";
  if (proposal.parent) {
    const slices = proposal.children.length;
    // 2026-09-17-0e79d: the parent draft IS the work ticket and the slices are the records —
    // the other way round from the shape this line was written for. It is the one line on the
    // card that counts what the button files, so it has to count what is actually filed.
    return `File this epic? One work ticket and ${slices} slice record${slices === 1 ? "" : "s"}.`;
  }
  if (proposal.phase) return "File this phase? One ticket.";
  return null;
}
