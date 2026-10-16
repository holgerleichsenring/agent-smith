import { RUN_END_KINDS, type TicketAttempt } from "@/types/tickets";
import { outcomePill } from "./AttemptCard";

// 2026-10-16-86e6d: the attempts before the newest, as ONE line in the state block and, behind Show, one row each —
// history the operator acts on only through the newest attempt. Words come from the end kind (86e6a) and the
// attempt's pill; an attempt that executed no spec stopped before any work.

const END_KIND_WORDS: Record<string, string> = {
  [RUN_END_KINDS.imageUntrusted]: "image not trusted",
  [RUN_END_KINDS.preflight]: "preflight failed",
  [RUN_END_KINDS.bootstrapMissing]: "bootstrap missing",
  [RUN_END_KINDS.criteriaUnmet]: "criterion not accepted",
  [RUN_END_KINDS.superseded]: "superseded",
};

export interface EarlierAttempts {
  line: string;
  rows: Array<{ attempt: TicketAttempt; words: string }>;
}

/** Every attempt but the newest, newest first; null when there is none. */
export function earlierAttempts(attempts: TicketAttempt[]): EarlierAttempts | null {
  const earlier = attempts.slice(0, -1).reverse();
  if (earlier.length === 0) return null;
  const head = `${earlier.length} earlier attempt${earlier.length === 1 ? "" : "s"}`;
  const before = earlier.every(stoppedBeforeWork) ? (earlier.length === 1 ? ", stopped before any work" : ", all stopped before any work") : "";
  const superseded = earlier.filter((a) => a.endKind === RUN_END_KINDS.superseded).map((a) => `, A${a.number} superseded`).join("");
  return { line: `${head}${before}${superseded}`, rows: earlier.map((attempt) => ({ attempt, words: attemptWords(attempt) })) };
}

/** "<pill words>", then " · <end-kind words>" only when an end kind adds something the pill does not say. */
export function attemptWords(attempt: TicketAttempt): string {
  const pill = outcomePill(attempt)[1];
  const kind = attempt.endKind ? END_KIND_WORDS[attempt.endKind] : undefined;
  return kind && kind !== pill ? `${pill} · ${kind}` : pill;
}

function stoppedBeforeWork(attempt: TicketAttempt): boolean {
  return attempt.executedSpecs.every((s) => s.status === "not_started");
}
