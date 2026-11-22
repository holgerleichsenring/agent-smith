import type { TicketAttempt, TicketExecutedSpec, TicketSeries, TicketSeriesSpec } from "@/types/tickets";
import { money } from "../ticketFormat";
import { elapsed } from "./flowWords";

// 2026-11-16-6877a: an attempt's report in the thread, from recorded fields, and the one line that leads to the run with
// its time and cost. 2026-11-22-7103a: what the run did is its status line (runStatusLine); the report's own sentence and
// the outcome pill it was drawn with are retired.

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

/** "5 of 5 done criteria met" for a spec stored with recorded verdicts; null for a spec without — 2026-11-19-1b72c: the
 *  card then shows no count at all. 2026-11-22-7103a: a spec with a master's report counts nothing (the report judged no
 *  line), and the count is drawn dim beside the spec's delivery word, never in a verdict colour. */
export function criteriaTally(spec: TicketSeriesSpec): string | null {
  if (spec.report || spec.verdictRecorded === false || spec.done.length === 0) return null;
  const met = spec.done.filter((c) => c.verdict === "met").length;
  return `${met} of ${spec.done.length} done criteria met`;
}

export type DeliveryTone = "ok" | "run" | "person" | "bad" | "quiet";

/** 2026-11-22-7103a: what came of one spec in one attempt, from that attempt's RunSpec row and the attempt's status —
 *  the words and colour its card heads with, and the status line agrees with. Null without a row. */
export function specDelivery(row: TicketExecutedSpec | undefined, attempt: TicketAttempt): { tone: DeliveryTone; words: string } | null {
  if (!row) return null;
  const live = attempt.status === "running" || attempt.status === "queued" || attempt.status === "waiting_for_input";
  switch (row.status) {
    case "done":
      if (attempt.status === "success" || attempt.status === "shortfall") return { tone: "ok", words: "delivered" };
      return live ? { tone: "ok", words: "verified" } : { tone: "bad", words: "verified, not delivered" };
    case "carried": return { tone: "ok", words: "carried" };
    case "handed_back": return { tone: "person", words: "handed back" };
    case "in_progress": return live ? { tone: "run", words: "working" } : { tone: "bad", words: "not delivered" };
    case "not_started": return live ? { tone: "quiet", words: "not started" } : { tone: "bad", words: "not started, not delivered" };
    default: return { tone: "bad", words: "not delivered" };
  }
}
