"use client";

import type { SpecDialogOpenQuestion } from "@/types/spec-dialog";
import { takeText } from "./turnProposal";

// 2026-11-10-d854b: the questions only the operator can answer, each with the design partner's proposal and the
// specs it touches. "Take proposal" sends the question and the proposal as the person's reply, words a reader on
// Slack understands too; "Answer" puts the question into the composer to answer differently. Without a composer
// to send through (the ticket page), the questions are shown without the two buttons.

export function TurnQuestions({
  questions,
  onTake,
  onAnswer,
}: {
  questions: SpecDialogOpenQuestion[];
  onTake?: (text: string) => void;
  onAnswer?: (question: string) => void;
}) {
  if (questions.length === 0) return null;
  return (
    <div className="flex flex-col gap-2" data-testid="turn-questions">
      <div className="d-turn-head">
        Needs you · {questions.length} {questions.length === 1 ? "question" : "questions"}
      </div>
      <div className="d-turn-box">
        {questions.map((question, index) => (
          <div key={index} className="d-turn-q" data-testid="turn-question">
            <div className="flex min-w-0 flex-[1_1_320px] flex-col gap-0.5">
              <span className="dsh-body font-semibold text-ink">{question.question}</span>
              <span className="dsh-body text-body">Proposal: {question.proposal}</span>
              {question.specIds.length > 0 && (
                <span className="dsh-label font-mono text-body">{question.specIds.join(" · ")}</span>
              )}
            </div>
            {onTake && onAnswer && (
              <div className="flex gap-2">
                <button type="button" className="btn primary" data-testid="turn-question-take"
                  onClick={() => onTake(takeText(question))}>
                  Take proposal
                </button>
                <button type="button" className="btn" data-testid="turn-question-answer"
                  onClick={() => onAnswer(question.question)}>
                  Answer
                </button>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
