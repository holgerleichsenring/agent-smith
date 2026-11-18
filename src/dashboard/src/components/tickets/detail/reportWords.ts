import { formatRunSummary } from "@/lib/formatRunSummary";
import type { TicketAttempt, TicketSeries, TicketSeriesSpec } from "@/types/tickets";
import { money } from "../ticketFormat";
import { elapsed } from "./flowWords";

// 2026-11-16-6877a: an attempt's report in the thread, from recorded fields: its state while it runs, else the run's
// own end text verbatim, and the one line that leads to the run with its time and cost.

export function reportText(attempt: TicketAttempt): string {
  const n = attempt.number;
  const said = attempt.summary ? formatRunSummary(attempt.summary).trim() : "";
  switch (attempt.status) {
    case "queued": return `Run ${n} waits in the queue.`;
    case "running": return `Run ${n} is working.`;
    case "waiting_for_input": return `Run ${n} is waiting for your answer.`;
  }
  if (attempt.handedBack) return said || `Run ${n} handed the ticket back.`;
  if (attempt.endedOnInfrastructure) return `Run ${n} ended on infrastructure: the sandbox or host was lost, not the work.${said ? `\n\n${said}` : ""}`;
  if (said) return said;
  switch (attempt.status) {
    case "success": return `Run ${n} finished.`;
    case "shortfall": return `Run ${n} finished with a shortfall.`;
    case "cancelled": return `Run ${n} was cancelled.`;
    default: return `Run ${n} stopped and left no reason.`;
  }
}

/** "Run 2 · 5m 55s · $0.42" — time and cost so far while it runs. */
export function runLine(attempt: TicketAttempt): string {
  const took = elapsed(attempt.startedAt, attempt.finishedAt);
  return [`Run ${attempt.number}`, took, money(attempt.cost)].filter(Boolean).join(" · ");
}

/** Whether the report reads as a stop: drawn in the stopped colour. */
export function reportStopped(attempt: TicketAttempt): boolean {
  return !attempt.handedBack && (attempt.status === "failed" || attempt.status === "error" || attempt.endedOnInfrastructure);
}

/** The specs this attempt executed, as the ticket's series records them. */
export function specsOf(attempt: TicketAttempt, series: TicketSeries | null): TicketSeriesSpec[] {
  return (series?.specs ?? [])
    .filter((s) => s.executedBy === attempt.runId || s.executedByAttempt === attempt.number)
    .sort((l, r) => l.ordinal - r.ordinal);
}

/** "5 of 5 done criteria met"; null when no verdict was recorded. */
export function criteriaTally(spec: TicketSeriesSpec): { words: string; all: boolean } | null {
  // 2026-11-18-98fa: a master that ended with finish_spec judged no line; its report is the verdict.
  if (spec.report) {
    const n = spec.followUps?.length ?? 0;
    return { words: n > 0 ? `reported · ${n} follow-up${n === 1 ? "" : "s"}` : "reported", all: true };
  }
  if (spec.verdictRecorded === false || spec.done.length === 0) return null;
  const met = spec.done.filter((c) => c.verdict === "met").length;
  return { words: `${met} of ${spec.done.length} done criteria met`, all: met === spec.done.length };
}
