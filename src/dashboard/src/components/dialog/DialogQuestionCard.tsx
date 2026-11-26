"use client";

import { useState } from "react";
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

interface Control {
  label: string;
  answer: string;
  primary: boolean;
  recommended?: boolean;
}

/** 2026-11-26-b186: past this many characters a clamped card folds its text — by length, which a test can check. */
const CLAMP_CHARS = 200;

export function DialogQuestionCard({
  question,
  onAnswer,
  recommended = null,
  hint,
  label,
  clamp = false,
}: {
  question: SpecDialogQuestionPush;
  onAnswer: (answer: string) => void;
  /** 2026-11-25-9992j: the index of the recommended choice, drawn as the primary button and marked so. */
  recommended?: number | null;
  /** 2026-11-25-9992j: what writing below does, where it is not the edit note of a proposal. */
  hint?: string;
  /** 2026-11-26-b186: what the card's label says in place of "waiting on you" (the series names who asks). */
  label?: string;
  /** 2026-11-26-b186: a long text shows its first lines with "show all"; the buttons always show. */
  clamp?: boolean;
}) {
  const t = useT();
  const [all, setAll] = useState(false);
  const long = clamp && question.text.length > CLAMP_CHARS;
  const expired = hasExpired(question);
  const controls = expired ? [] : controlsFor(t, question, recommended);
  return (
    <div
      data-testid="dialog-question"
      data-kind={question.kind}
      className={expired ? "ecard inert d-body text-body" : "ecard inert waiting d-body"}
    >
      <div className={expired ? "fl mb-1" : "fl on mb-1"}>
        {expired ? t("dialog.question.expired") : label ?? t("dialog.question.waiting")}
      </div>
      {question.text.trim().length > 0 && (
        <div className={long && !all ? "dq-clamp" : undefined} data-testid="dialog-question-text"><Markdown>{question.text}</Markdown></div>
      )}
      {long && (
        <button type="button" className="d-link dq-more" aria-expanded={all} onClick={() => setAll((was) => !was)}
          data-testid="dialog-question-more">
          {t(all ? "dialog.question.showLess" : "dialog.question.showAll")}
        </button>
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
            {control.recommended && <span className="ml-1 opacity-80">({t("dialog.question.recommended")})</span>}
          </button>
        ))}
        {/* 2026-09-17-042ek: an EXPIRED card promised a revision it cannot deliver — the
            confirmer has stopped waiting, so the next message buys a whole design turn instead. */}
        <span className="ec-sub min-w-44 flex-1">
          {expired
            ? t("dialog.question.expiredHint")
            : hint ?? t("dialog.question.noteHint")}
        </span>
      </div>
    </div>
  );
}

function controlsFor(t: T, question: SpecDialogQuestionPush, recommended: number | null): Control[] {
  if (question.kind === "approval") return [];
  if (question.choices.length > 0) return question.choices.map((choice, at) => asControl(choice, at === recommended));
  if (question.kind === "confirmation")
    return [
      { label: t("dialog.question.yes"), answer: "yes", primary: true },
      { label: t("dialog.question.no"), answer: "no", primary: false },
    ];
  // Free text and info questions are answered by writing, which the composer already does.
  return [];
}

function asControl(choice: SpecDialogChoice, recommended: boolean): Control {
  return { label: choice.label, answer: choice.label, primary: recommended, recommended };
}
