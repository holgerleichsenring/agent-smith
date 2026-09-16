"use client";

import { Markdown } from "@/components/ui/Markdown";
import type { SpecDialogChoice, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-09-15-cb3e: the affordance a typed question needs. The controls come from the
// question itself — a Choice question carries its choices, and the APPROVAL that files
// tickets carries none at all: every adapter builds the approve/reject pair from the kind,
// which is why the kind travels with the push.
//
// An answer goes back as an ordinary message through the same endpoint the composer uses,
// so a click and a typed reply take one path — and anything else the operator types is the
// edit note the flow already routes.

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
  onAnswer,
}: {
  question: SpecDialogQuestionPush;
  onAnswer: (answer: string) => void;
}) {
  const expired = hasExpired(question);
  const controls = expired ? [] : controlsFor(question);
  return (
    <div
      data-testid="dialog-question"
      className={expired
        ? "rounded border border-stone-300 bg-stone-50 px-3 py-2 text-stone-600"
        : "rounded border border-amber-300 bg-amber-50 px-3 py-2"}
    >
      <div className={expired
        ? "mb-1 dsh-label uppercase tracking-wide text-stone-500"
        : "mb-1 dsh-label uppercase tracking-wide text-amber-700"}>
        {expired ? "no longer waiting — nothing was filed" : "waiting on you"}
      </div>
      <Markdown>{question.text}</Markdown>
      <div className="mt-2 flex flex-wrap gap-2">
        {controls.map((control) => (
          <button
            key={control.answer}
            type="button"
            data-testid={`dialog-answer-${control.answer}`}
            onClick={() => onAnswer(control.answer)}
            className={
              control.primary
                ? "rounded bg-emerald-700 px-3 py-1 text-sm text-white hover:bg-emerald-800"
                : "rounded border border-stone-300 px-3 py-1 text-sm text-stone-700 hover:bg-stone-100"
            }
          >
            {control.label}
          </button>
        ))}
      </div>
      <p className="mt-2 text-xs text-[var(--color-ink-mid)]">
        Anything else you write below is a note — the proposal is revised with it.
      </p>
    </div>
  );
}

function controlsFor(question: SpecDialogQuestionPush): Control[] {
  if (question.choices.length > 0) return question.choices.map(asControl);
  if (question.kind === "approval")
    return [
      { label: "Approve", answer: "approve", primary: true },
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
