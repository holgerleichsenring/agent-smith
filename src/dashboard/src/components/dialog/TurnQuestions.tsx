"use client";

import { useEffect, useState } from "react";
import type { SpecDialogOpenQuestion } from "@/types/spec-dialog";
import { answersText, loadHeld, ownAnswers, saveHeld, type HeldQuestions } from "./turnQuestionsHeld";

// 2026-11-10-d854b: the questions only the operator can answer, each with the design partner's proposal and the
// specs it touches. 2026-11-12-e2f5: nothing here sends on a click. "Take proposal" marks a question taken and
// "Answer" opens a field whose text is held; one row below says what happens next — Approve & file files every
// question on its proposal, and once an own answer is held, "Send N answers" sends them all as ONE message, so one
// turn. Without a conversation to send to (the ticket page), the questions are shown without the controls.

export function TurnQuestions({
  questions,
  heldKey,
  onSend,
}: {
  questions: SpecDialogOpenQuestion[];
  /** The proposal id the held state is kept under across a reload; none keeps it only while the page is open. */
  heldKey?: string;
  onSend?: (text: string) => void;
}) {
  const [held, setHeld] = useState<HeldQuestions>(() => loadHeld(heldKey));
  useEffect(() => saveHeld(heldKey, held), [heldKey, held]);
  if (questions.length === 0) return null;
  const set = (index: number, entry: HeldQuestions[number] | null) => setHeld((was) => {
    const next = { ...was };
    if (entry) next[index] = entry;
    else delete next[index];
    return next;
  });
  const answers = ownAnswers(held);
  const send = () => {
    onSend?.(answersText(questions, held));
    setHeld({});
  };
  return (
    <div className="flex flex-col gap-2" data-testid="turn-questions">
      <div className="d-turn-head">
        Needs you · {questions.length} {questions.length === 1 ? "question" : "questions"}
      </div>
      <div className="d-turn-box">
        {questions.map((question, index) => {
          const entry = held[index];
          const taken = entry !== undefined && "taken" in entry;
          return (
            <div key={index} className="d-turn-q" data-testid="turn-question" data-held={taken ? "taken" : entry ? "answer" : "open"}>
              <div className="flex min-w-0 flex-[1_1_320px] flex-col gap-0.5">
                <span className="dsh-body font-semibold text-ink">{question.question}</span>
                <span className="dsh-body text-body">Proposal: {question.proposal}</span>
                {question.specIds.length > 0 && (
                  <span className="dsh-label font-mono text-body">{question.specIds.join(" · ")}</span>
                )}
                {entry && "answer" in entry && (
                  <textarea className="dsh-body mt-1 w-full rounded border px-2 py-1" rows={2} autoFocus
                    aria-label={`Your answer: ${question.question}`} data-testid="turn-question-field"
                    value={entry.answer} onChange={(event) => set(index, { answer: event.target.value })} />
                )}
              </div>
              {onSend && (
                <div className="flex gap-2">
                  {taken ? (
                    <button type="button" className="btn" data-testid="turn-question-undo" onClick={() => set(index, null)}>
                      ✓ Taken · Undo
                    </button>
                  ) : entry ? (
                    <button type="button" className="btn" data-testid="turn-question-drop" onClick={() => set(index, null)}>
                      Drop answer
                    </button>
                  ) : (
                    <>
                      <button type="button" className="btn primary" data-testid="turn-question-take"
                        onClick={() => set(index, { taken: true })}>
                        Take proposal
                      </button>
                      <button type="button" className="btn" data-testid="turn-question-answer"
                        onClick={() => set(index, { answer: "" })}>
                        Answer
                      </button>
                    </>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
      {onSend && (
        <div className="flex flex-wrap items-center gap-2" data-testid="turn-questions-actions" data-state={answers > 0 ? "answers" : "proposals"}>
          {answers > 0 ? (
            <>
              <button type="button" className="btn primary" data-testid="turn-questions-send" onClick={send}>
                Send {answers} {answers === 1 ? "answer" : "answers"}
              </button>
              <span className="dsh-body text-body">One message; taken proposals go with it, untouched questions stay open.</span>
            </>
          ) : (
            <>
              <button type="button" className="btn" data-testid="turn-questions-take-all"
                onClick={() => setHeld(Object.fromEntries(questions.map((_, index) => [index, { taken: true }])))}>
                Take all proposals
              </button>
              <span className="dsh-body text-body">Approve &amp; file decides every question on its proposal.</span>
            </>
          )}
        </div>
      )}
    </div>
  );
}
