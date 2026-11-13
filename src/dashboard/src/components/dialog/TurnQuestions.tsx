import type { SpecDialogOpenQuestion } from "@/types/spec-dialog";

// 2026-11-10-d854b: the questions only the operator can answer, each with the design partner's proposal and the
// specs it touches. 2026-11-12-92e9c: plain numbered text, on the conversation and on the ticket page alike — the
// operator answers in the composer, in their own words, and nothing here holds or sends anything.

export function TurnQuestions({ questions }: { questions: SpecDialogOpenQuestion[] }) {
  if (questions.length === 0) return null;
  return (
    <div className="flex flex-col gap-1.5" data-testid="turn-questions">
      <div className="d-turn-head">
        Open {questions.length === 1 ? "question" : "questions"} · {questions.length}
      </div>
      <ol className="flex flex-col gap-2 dsh-body">
        {questions.map((question, index) => (
          <li key={index} className="flex flex-col gap-0.5" data-testid="turn-question">
            <span className="text-ink">{index + 1}. {question.question}</span>
            <span className="pl-4 text-body">Proposal: {question.proposal}</span>
            {question.specIds.length > 0 && (
              <span className="pl-4 dsh-label font-mono text-mute">{question.specIds.join(" · ")}</span>
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}
