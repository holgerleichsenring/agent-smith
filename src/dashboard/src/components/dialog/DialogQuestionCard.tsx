"use client";

import { useT } from "@/hooks/useT";
import { Markdown } from "@/components/ui/Markdown";
import type { T } from "@/lib/i18n/translate";
import { hasExpired } from "./approvalDecision";
import type { SpecDialogChoice, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-09-15-cb3e: the affordance a typed question needs. The controls come from the
// question itself — a Choice question carries its choices, a confirmation is Yes/No.
//
// An answer goes back as an ordinary message through the same endpoint the composer uses,
// so a click and a typed reply take one path — and anything else the operator types is the
// edit note the flow already routes.
//
// 2026-09-17-042ef: the card is the Projects page's panel card, and the accent line around it
// is this page's own modifier — it says the conversation is waiting on a person, which is a
// state the studio's cards never have. An expired card keeps the plain card line, because
// nothing is waiting any more.
//
// 2026-11-19-1b72c: an APPROVAL is no longer decided on this card. An offered proposal takes the
// composer's place as a decision (DialogDecisionBar, 7c42b) on both pages, and discarding is said in
// words, so the card's Approve/Reject pair and its shape buttons are gone. An approval reaches the
// card only once its wait is over, to say nothing was filed.
//
// 2026-11-27-3532e: the series' question card that set a label, a hint, a recommendation and a fold is gone (the master's
// question is a chat message with quick replies), and those props went with it.

interface Control {
  label: string;
  answer: string;
  primary: boolean;
}

export function DialogQuestionCard({ question, onAnswer }: { question: SpecDialogQuestionPush; onAnswer: (answer: string) => void }) {
  const t = useT();
  const expired = hasExpired(question);
  const controls = expired ? [] : controlsFor(t, question);
  return (
    <div
      data-testid="dialog-question"
      data-kind={question.kind}
      className={expired ? "ecard inert d-body text-body" : "ecard inert waiting d-body"}
    >
      <div className={expired ? "fl mb-1" : "fl on mb-1"}>
        {expired ? t("dialog.question.expired") : t("dialog.question.waiting")}
      </div>
      {question.text.trim().length > 0 && (
        <div data-testid="dialog-question-text"><Markdown>{question.text}</Markdown></div>
      )}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {controls.map((control) => (
          <button
            key={control.answer}
            type="button"
            data-testid={`dialog-answer-${control.answer}`}
            onClick={() => onAnswer(control.answer)}
            className={control.primary ? "btn primary" : "btn"}
          >
            {control.label}
          </button>
        ))}
        {/* 2026-09-17-042ek: an EXPIRED card promised a revision it cannot deliver — the
            confirmer has stopped waiting, so the next message buys a whole design turn instead. */}
        <span className="ec-sub min-w-44 flex-1">
          {expired
            ? t("dialog.question.expiredHint")
            : t("dialog.question.noteHint")}
        </span>
      </div>
    </div>
  );
}

function controlsFor(t: T, question: SpecDialogQuestionPush): Control[] {
  if (question.kind === "approval") return [];
  if (question.choices.length > 0) return question.choices.map(asControl);
  if (question.kind === "confirmation")
    return [
      { label: t("dialog.question.yes"), answer: "yes", primary: true },
      { label: t("dialog.question.no"), answer: "no", primary: false },
    ];
  // Free text and info questions are answered by writing, which the composer already does.
  return [];
}

function asControl(choice: SpecDialogChoice): Control {
  return { label: choice.label, answer: choice.label, primary: false };
}
