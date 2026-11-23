import type { T } from "@/lib/i18n/translate";
import type { SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-23-31dcd: one ticket's state on the Tickets section, from its row alone — merged, then a pull request that
// waits, then its newest attempt, else not started.

const WAITS = new Set(["open", "unread"]);

export function seriesTicketState(ticket: SeriesRunTicket, t: T): { text: string; tone: "ok" | "run" | "need" | "bad" | "idle" } {
  if (ticket.landed) return { text: t("series.ticket.merged"), tone: "ok" };
  const waiting = ticket.pullRequests.filter((p) => WAITS.has(p.state));
  if (waiting.length > 0) return { text: t("series.ticket.waiting", { numbers: waiting.map((p) => `#${p.number}`).join(" + ") }), tone: "run" };
  switch (ticket.status) {
    case null: return { text: t("series.ticket.notStarted"), tone: "idle" };
    case "running":
    case "queued": return { text: t("series.ticket.working"), tone: "run" };
    case "waiting_for_input": return { text: t("series.ticket.asking"), tone: "need" };
    case "failed": return { text: t("series.ticket.failed"), tone: "bad" };
    case "success": return { text: t("series.ticket.done"), tone: "idle" };
    case "cancelled": return { text: t("series.ticket.cancelled"), tone: "idle" };
    case "shortfall": return { text: t("series.ticket.shortfall"), tone: "bad" };
    default: return { text: t("series.ticket.ended"), tone: "idle" };
  }
}
