import { summaryLine } from "@/lib/formatRunSummary";
import { runPath } from "@/lib/runPath";
import type { TicketAttempt, TicketDerivation, TicketOtherRun } from "@/types/tickets";
import { money } from "../ticketFormat";
import { attemptWords, outcomeTone, type OutcomeTone } from "./attemptOutcome";
import { clock, elapsed } from "./flowWords";
import { reportText } from "./reportWords";
import { derivationWords } from "./threadItems";

// 2026-11-17-8d1cc: the Runs section (TicketRail mock) — every attempt and every spec derivation of the ticket in one
// line, newest first: how it ended with its end kind, its own first line, when it started, how long, what it cost, and
// the run it opens. Replaces the attempt cards; the run page holds each run's whole story.

export interface RunListLine {
  runId: string;
  who: string;
  outcome: string;
  text: string;
  /** "Nov 13 06:05 · 5m 55s · $1.13" */
  meta: string;
  tone: OutcomeTone;
  href: string;
  /** Set for an attempt: what started it is drawn under its line. */
  attempt: TicketAttempt | null;
  startedAt: string;
}

export function attemptListLine(attempt: TicketAttempt): RunListLine {
  return {
    runId: attempt.runId,
    who: `Run ${attempt.number}`,
    outcome: attemptWords(attempt),
    text: summaryLine(attempt.summary) ?? reportText(attempt),
    meta: [clock(attempt.startedAt), elapsed(attempt.startedAt, attempt.finishedAt), money(attempt.cost)].filter(Boolean).join(" · "),
    tone: outcomeTone(attempt),
    href: runPath(attempt.runId),
    attempt,
    startedAt: attempt.startedAt,
  };
}

export function derivationListLine(derivation: TicketDerivation): RunListLine {
  const running = derivation.outcome === "running";
  return {
    runId: derivation.runId,
    who: "Derivation",
    outcome: running ? "running" : derivation.outcome === "not-finished" ? "not finished" : derivation.outcome,
    text: `Spec derivation ${derivationWords(derivation)}.`,
    meta: [clock(derivation.startedAt), elapsed(derivation.startedAt, derivation.finishedAt),
      running ? `${money(derivation.cost)} so far` : money(derivation.cost)].filter(Boolean).join(" · "),
    tone: running ? "run" : derivation.outcome === "not-finished" ? "bad" : derivation.outcome === "asked" ? "person" : "ok",
    href: runPath(derivation.runId),
    attempt: null,
    startedAt: derivation.startedAt,
  };
}

/** 2026-11-17-3435b: a merge-green run — the CI loop merging the attempt's pull requests once CI was green. */
export function mergeListLine(run: TicketOtherRun): RunListLine {
  const running = run.finishedAt === null;
  const ok = run.status === "success";
  return {
    runId: run.runId,
    who: "Merge",
    outcome: running ? "running" : ok ? "done" : "not finished",
    text: "CI was green; merge the pull requests.",
    meta: [clock(run.startedAt), elapsed(run.startedAt, run.finishedAt), money(run.cost)].filter(Boolean).join(" · "),
    tone: running ? "run" : ok ? "ok" : "bad",
    href: runPath(run.runId),
    attempt: null,
    startedAt: run.startedAt,
  };
}

/** Every attempt, derivation and merge-green run, newest start first. */
export function runListLines(attempts: readonly TicketAttempt[], derivations: readonly TicketDerivation[],
  otherRuns: readonly TicketOtherRun[] = []): RunListLine[] {
  return [...attempts.map(attemptListLine), ...derivations.map(derivationListLine),
    ...otherRuns.filter((r) => r.pipeline === "merge-green").map(mergeListLine)]
    .sort((l, r) => Date.parse(r.startedAt) - Date.parse(l.startedAt) || (l.runId < r.runId ? 1 : -1));
}
