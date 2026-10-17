import { formatRunSummary } from "@/lib/formatRunSummary";
import { runPath } from "@/lib/runPath";
import { RUN_END_KINDS, type TicketAttempt } from "@/types/tickets";
import { outcomePill } from "./AttemptCard";

// 2026-10-16-7ed3b: every attempt as one small chip in the state block — A1…An oldest first, in its outcome's
// tone, each opening its run, its cause as the title. Replaces 86e6d's earlier-attempts disclosure.

const END_KIND_WORDS: Record<string, string> = {
  [RUN_END_KINDS.imageUntrusted]: "image not trusted",
  [RUN_END_KINDS.preflight]: "preflight failed",
  [RUN_END_KINDS.bootstrapMissing]: "bootstrap missing",
  [RUN_END_KINDS.criteriaUnmet]: "criterion not accepted",
  [RUN_END_KINDS.superseded]: "superseded",
};

export interface AttemptChip {
  attempt: TicketAttempt;
  tone: string;
  href: string;
  title: string;
  head: boolean;
}

/** One chip per attempt, oldest first; empty when the ticket has fewer than two attempts. */
export function attemptChips(attempts: TicketAttempt[]): AttemptChip[] {
  if (attempts.length < 2) return [];
  const last = attempts.length - 1;
  return attempts.map((attempt, i) => ({
    attempt,
    tone: outcomePill(attempt)[0],
    href: runPath(attempt.runId),
    title: chipTitle(attempt),
    head: i === last,
  }));
}

function chipTitle(attempt: TicketAttempt): string {
  const cause = attempt.summary ? firstLine(formatRunSummary(attempt.summary)) : null;
  return `A${attempt.number} · ${attemptWords(attempt)}${cause ? ` — ${cause}` : ""}`;
}

/** The first non-empty line of a text, trimmed; null when there is none. */
export function firstLine(text: string): string | null {
  return text.split(/\r?\n/).map((l) => l.trim()).find((l) => l.length > 0) ?? null;
}

/** "<pill words>", then " · <end-kind words>" only when an end kind adds something the pill does not say. */
export function attemptWords(attempt: TicketAttempt): string {
  const pill = outcomePill(attempt)[1];
  const kind = attempt.endKind ? END_KIND_WORDS[attempt.endKind] : undefined;
  return kind && kind !== pill ? `${pill} · ${kind}` : pill;
}
