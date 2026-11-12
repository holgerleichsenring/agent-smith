import type { SpecDialogOpenQuestion } from "@/types/spec-dialog";

// 2026-11-12-e2f5: what the operator did with a turn's open questions, held on the page until one send. A taken
// question files on its proposal; an answer is the operator's own and goes back to the design partner, all of them
// in one message, so answering five questions is one turn and not five.

export type HeldAnswer = { taken: true } | { answer: string };

/** Question index -> what the operator did with it; an untouched question has no entry. */
export type HeldQuestions = Record<number, HeldAnswer>;

const PREFIX = "turn-questions:";

export function ownAnswers(held: HeldQuestions): number {
  return Object.values(held).filter((entry) => "answer" in entry && entry.answer.trim().length > 0).length;
}

/** The one message "Send N answers" sends: every own answer, and "take your proposal" for each taken question. */
export function answersText(questions: SpecDialogOpenQuestion[], held: HeldQuestions): string {
  const lines = questions.flatMap((question, index) => {
    const entry = held[index];
    if (!entry) return [];
    if ("taken" in entry) return [`${index + 1}. ${question.question} — take your proposal (${question.proposal})`];
    return entry.answer.trim() ? [`${index + 1}. ${question.question} — ${entry.answer.trim()}`] : [];
  });
  return ["My answers to the open questions:", ...lines].join("\n");
}

/** The held state of one proposal, or nothing when storage is unavailable or holds none. */
export function loadHeld(key: string | undefined): HeldQuestions {
  if (!key) return {};
  try {
    const raw = window.localStorage.getItem(PREFIX + key);
    return raw ? (JSON.parse(raw) as HeldQuestions) : {};
  } catch {
    return {};
  }
}

export function saveHeld(key: string | undefined, held: HeldQuestions): void {
  if (!key) return;
  try {
    if (Object.keys(held).length === 0) window.localStorage.removeItem(PREFIX + key);
    else window.localStorage.setItem(PREFIX + key, JSON.stringify(held));
  } catch {
    // A page without storage keeps the state for as long as it is open.
  }
}
