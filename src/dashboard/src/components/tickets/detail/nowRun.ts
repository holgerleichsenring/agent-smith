import type { TicketAttempt, TicketDerivation, TicketDetail } from "@/types/tickets";

// 2026-11-22-4321a: the run the Now section draws open — the attempt whose question waits, else the newest attempt;
// beside it a spec derivation that is running, or the newest derivation of a ticket with no attempt.

export interface NowRun {
  attempt: TicketAttempt | null;
  derivation: TicketDerivation | null;
}

export function nowRun(detail: TicketDetail): NowRun {
  const attempts = [...detail.attempts].sort((l, r) => l.number - r.number);
  const asking = [...attempts].reverse().find((a) => a.pendingQuestion) ?? null;
  const attempt = asking ?? attempts[attempts.length - 1] ?? null;
  const derivations = [...(detail.derivations ?? [])].sort((l, r) => Date.parse(l.startedAt) - Date.parse(r.startedAt));
  const running = derivations.filter((d) => d.outcome === "running");
  const derivation = running[running.length - 1] ?? (attempt ? null : derivations[derivations.length - 1] ?? null);
  return { attempt, derivation };
}
