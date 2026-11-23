import { attemptStatus, statusRest, type StatusContext } from "@/components/jobs/runlog/runStatusLine";
import type { T, TextKey } from "@/lib/i18n/translate";
import { runPath } from "@/lib/runPath";
import type { TicketAttempt, TicketDerivation, TicketOtherRun } from "@/types/tickets";
import { money } from "../ticketFormat";
import type { StatusTone as OutcomeTone } from "@/components/jobs/runlog/runStatusLine";
import { clock, elapsed } from "./flowWords";
import { derivationWords } from "./threadItems";

// 2026-11-17-8d1cc: the Runs section (TicketRail mock) — every attempt and every spec derivation of the ticket in one
// line, newest first: how it ended with its end kind, its own first line, when it started, how long, what it cost, and
// the run it opens. Replaces the attempt cards; the run page holds each run's whole story.

/** 2026-11-23-5669b: a finished derivation's outcome as the row words it; an unknown one is shown as recorded. */
const DERIVATION_OUTCOMES: Record<string, TextKey> = {
  derived: "tickets.runs.derived", asked: "tickets.runs.asked",
};

export interface RunListLine {
  runId: string;
  who: string;
  outcome: string;
  text: string;
  /** "Nov 13 06:05 · 5m 55s · $1.13" */
  meta: string;
  tone: OutcomeTone;
  /** Still running: a deploy's delivery is drawn only once it ended. */
  running: boolean;
  href: string;
  /** 2026-11-23-5669b: what the line is, so a check never reads its (translated) words. */
  kind: "attempt" | "derivation" | "merge" | "deploy";
  /** Set for an attempt: what started it is drawn under its line. */
  attempt: TicketAttempt | null;
  startedAt: string;
}

/** 2026-11-22-7103a: an attempt's outcome and text are its status line's head and clauses, as its row on Now says. */
export function attemptListLine(
  t: T, locale: string, attempt: TicketAttempt, ctx: StatusContext | null = null, queuePlace: number | null = null,
): RunListLine {
  const status = attemptStatus(t, attempt, ctx, queuePlace);
  return {
    runId: attempt.runId,
    kind: "attempt",
    who: t("tickets.story.run", { n: attempt.number }),
    outcome: status.head,
    text: statusRest(status),
    meta: [clock(attempt.startedAt, locale), elapsed(t, attempt.startedAt, attempt.finishedAt), money(attempt.cost)].filter(Boolean).join(" · "),
    tone: status.tone,
    running: attempt.finishedAt === null,
    href: runPath(attempt.runId),
    attempt,
    startedAt: attempt.startedAt,
  };
}

export function derivationListLine(t: T, locale: string, derivation: TicketDerivation): RunListLine {
  const running = derivation.outcome === "running";
  return {
    runId: derivation.runId,
    kind: "derivation",
    who: t("tickets.story.derivation"),
    outcome: running ? t("tickets.runs.running") : derivation.outcome === "not-finished" ? t("tickets.runs.notFinished")
      : DERIVATION_OUTCOMES[derivation.outcome] ? t(DERIVATION_OUTCOMES[derivation.outcome]) : derivation.outcome,
    text: t("tickets.story.derivationText", { words: derivationWords(t, derivation) }),
    meta: [clock(derivation.startedAt, locale), elapsed(t, derivation.startedAt, derivation.finishedAt),
      running ? t("tickets.story.soFar", { cost: money(derivation.cost) }) : money(derivation.cost)].filter(Boolean).join(" · "),
    tone: running ? "run" : derivation.outcome === "not-finished" ? "bad" : derivation.outcome === "asked" ? "person" : "ok",
    running,
    href: runPath(derivation.runId),
    attempt: null,
    startedAt: derivation.startedAt,
  };
}

/** 2026-11-17-3435b: a merge-green run — the CI loop merging the attempt's pull requests once CI was green. */
export function mergeListLine(t: T, locale: string, run: TicketOtherRun): RunListLine {
  const running = run.finishedAt === null;
  const ok = run.status === "success";
  return {
    runId: run.runId,
    kind: "merge",
    who: t("tickets.runs.merge"),
    outcome: running ? t("tickets.runs.running") : ok ? t("tickets.runs.done") : t("tickets.runs.notFinished"),
    text: t("tickets.runs.mergeText"),
    meta: [clock(run.startedAt, locale), elapsed(t, run.startedAt, run.finishedAt), money(run.cost)].filter(Boolean).join(" · "),
    tone: running ? "run" : ok ? "ok" : "bad",
    running,
    href: runPath(run.runId),
    attempt: null,
    startedAt: run.startedAt,
  };
}

/** 2026-11-18-3863f: a deploy run started from this ticket; where its delivery stands is drawn under it. */
export function deployListLine(t: T, locale: string, run: TicketOtherRun): RunListLine {
  const running = run.finishedAt === null;
  const ok = run.status === "success";
  return {
    runId: run.runId,
    kind: "deploy",
    who: t("tickets.runs.deploy"),
    outcome: running ? t("tickets.runs.running") : ok ? t("tickets.report.delivered") : t("tickets.report.notDelivered"),
    text: t("tickets.runs.deployText"),
    meta: [clock(run.startedAt, locale), elapsed(t, run.startedAt, run.finishedAt), money(run.cost)].filter(Boolean).join(" · "),
    tone: running ? "run" : ok ? "ok" : "bad",
    running,
    href: runPath(run.runId),
    attempt: null,
    startedAt: run.startedAt,
  };
}

/** Every attempt, derivation, merge-green and deploy run, newest start first. */
export function runListLines(t: T, locale: string, attempts: readonly TicketAttempt[], derivations: readonly TicketDerivation[],
  otherRuns: readonly TicketOtherRun[] = [], ctx: StatusContext | null = null, queuePlace: number | null = null): RunListLine[] {
  return [...attempts.map((a) => attemptListLine(t, locale, a, ctx, queuePlace)), ...derivations.map((d) => derivationListLine(t, locale, d)),
    ...otherRuns.filter((r) => r.pipeline === "merge-green").map((r) => mergeListLine(t, locale, r)),
    ...otherRuns.filter((r) => r.pipeline === "deploy").map((r) => deployListLine(t, locale, r))]
    .sort((l, r) => Date.parse(r.startedAt) - Date.parse(l.startedAt) || (l.runId < r.runId ? 1 : -1));
}
