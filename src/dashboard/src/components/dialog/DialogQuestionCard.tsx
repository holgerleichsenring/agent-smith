"use client";

import { Markdown } from "@/components/ui/Markdown";
import type {
  SpecDialogChoice,
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
}

export function DialogQuestionCard({
  question,
  proposal,
  onAnswer,
}: {
  question: SpecDialogQuestionPush;
  proposal: SpecDialogProposalPush | null;
  onAnswer: (answer: string) => void;
}) {
  const expired = hasExpired(question);
  const controls = expired ? [] : controlsFor(question);
  const decision = question.kind === "approval" && !expired;
  const summary = decision && proposal ? summaryOf(proposal) : null;
  return (
    <div
      data-testid="dialog-question"
      data-kind={question.kind}
      className={
        expired
          ? "rounded-md border border-mute bg-canvas-soft px-3 py-2.5 text-body"
          : "rounded-md border border-primary-deep bg-canvas-soft px-3 py-2.5"
      }
    >
      <div className={expired ? "mb-1 eyebrow-uppercase text-body" : "mb-1 eyebrow-uppercase text-primary-deep"}>
        {expired ? "no longer waiting — nothing was filed" : "waiting on you"}
      </div>
      {summary && (
        <p data-testid="dialog-approval-summary" className="dsh-body font-semibold text-ink">
          {summary}
        </p>
      )}
      <Markdown>{question.text}</Markdown>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {controls.map((control) => (
          <button
            key={control.answer}
            type="button"
            data-testid={`dialog-answer-${control.answer}`}
            onClick={() => onAnswer(control.answer)}
            className={
              control.primary
                ? "rounded-md bg-primary-deep px-3 py-1.5 dsh-body font-semibold text-on-primary hover:bg-primary-pressed"
                : "rounded-md border border-mute bg-canvas px-3 py-1.5 dsh-body font-semibold text-ink hover:bg-canvas-soft"
            }
          >
            {control.label}
          </button>
        ))}
        <span className="min-w-44 flex-1 dsh-label text-body">
          {decision
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
      { label: "Approve & file", answer: "approve", primary: true },
      { label: "Reject", answer: "reject", primary: false },
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
    return `File this epic? A parent record and ${slices} slice${slices === 1 ? "" : "s"}.`;
  }
  if (proposal.phase) return "File this phase? One ticket.";
  return null;
}
