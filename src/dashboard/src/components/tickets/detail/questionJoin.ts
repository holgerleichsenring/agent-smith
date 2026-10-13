import type { RunPass, RunPasses, TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59q: neither a pass nor a question entry carries the other's id. Each question park
// ends exactly one pass ("question") and records one question entry, so the k-th question-ended pass
// of the run, in run-wide order, IS the k-th question entry of that run. When the counts differ the
// record is not whole and nothing is paired — a time-matched guess would put words on the wrong park.

export interface QuestionPair {
  pass: RunPass;
  question: TicketConversationEntry;
  answer: TicketConversationEntry | null;
  /** Answer at minus question at; null while unanswered. */
  parkedMs: number | null;
}

export interface QuestionJoin {
  /** By the pass's run-wide ordinal. Empty unless the counts agree. */
  pairs: ReadonlyMap<number, QuestionPair>;
  matched: boolean;
}

/** The attempt's question entries, in the order the conversation read serves them. */
export function questionEntries(entries: TicketConversationEntry[]): TicketConversationEntry[] {
  return entries.filter((e) => e.source === "question" && e.kind === "question");
}

export function questionJoin(passes: RunPasses | null, entries: TicketConversationEntry[]): QuestionJoin {
  const parked = (passes?.specs.flatMap((s) => s.passes) ?? [])
    .filter((p) => p.endedBecause === "question")
    .sort((l, r) => l.ordinal - r.ordinal);
  const asked = questionEntries(entries);
  if (parked.length !== asked.length) return { pairs: new Map(), matched: false };
  const pairs = new Map<number, QuestionPair>();
  parked.forEach((pass, k) => {
    const question = asked[k];
    const answer = entries.find((e) => e.id === `${question.id}:answer`) ?? null;
    const parkedMs = answer ? Date.parse(answer.at) - Date.parse(question.at) : null;
    pairs.set(pass.ordinal, { pass, question, answer, parkedMs: Number.isNaN(parkedMs) ? null : parkedMs });
  });
  return { pairs, matched: true };
}
