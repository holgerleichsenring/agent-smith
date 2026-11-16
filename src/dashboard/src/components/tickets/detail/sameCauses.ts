import type { TicketAttempt } from "@/types/tickets";

// 2026-10-17-e730e: a failed attempt whose served end kind and summary equal the one before it continues that run of
// equal causes. 2026-11-16-6877a: the thread folds such a run into one line with a count (threadItems).

const FAILED = new Set(["failed", "error"]);

/** Both failed, both summaries non-empty and equal as strings, and equal end kinds (absent read as null). */
export function sameCause(a: TicketAttempt, b: TicketAttempt): boolean {
  return FAILED.has(a.status) && FAILED.has(b.status)
    && !!a.summary && a.summary === b.summary
    && (a.endKind ?? null) === (b.endKind ?? null);
}

/** Per run id, the first attempt of the run of equal causes it continues. */
export function sameCauses(attempts: TicketAttempt[]): Map<string, { number: number; key: string }> {
  const ordered = [...attempts].sort((x, y) => x.number - y.number);
  const result = new Map<string, { number: number; key: string }>();
  for (let i = 1; i < ordered.length; i++) {
    const before = ordered[i - 1];
    const attempt = ordered[i];
    if (before.number !== attempt.number - 1 || !sameCause(attempt, before)) continue;
    result.set(attempt.runId, result.get(before.runId) ?? { number: before.number, key: before.runId });
  }
  return result;
}
