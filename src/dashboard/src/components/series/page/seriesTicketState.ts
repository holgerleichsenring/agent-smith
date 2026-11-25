import type { T } from "@/lib/i18n/translate";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import { waitingPullRequests } from "./seriesNodeKind";

// 2026-11-23-31dcd: one ticket's state on the Tickets section. 2026-11-24-f9c5f: in a series with a branch, merged means into
// that branch. 2026-11-25-9992a: rendered from the server's one kind (ticket.kind) — the same the Overview colours and the
// master reads — in the viewer's language; the newest attempt only picks the word inside a kind, never the kind.

type Tone = "ok" | "run" | "need" | "bad" | "idle";

export function seriesTicketState(ticket: SeriesRunTicket, t: T, seriesBranch?: string | null): { text: string; tone: Tone } {
  const waiting = waitingPullRequests(ticket.pullRequests);
  switch (ticket.kind) {
    case "merged": return { text: t(seriesBranch ? "series.ticket.mergedIntoSeries" : "series.ticket.merged"), tone: "ok" };
    case "running": return { text: t("series.ticket.working"), tone: "run" };
    case "question": return { text: t("series.ticket.asking"), tone: "need" };
    case "failed": return { text: t(ticket.status === "shortfall" ? "series.ticket.shortfall" : "series.ticket.failed"), tone: "bad" };
    case "now":
    case "needed":
    case "pr":
      if (waiting.length > 0) return { text: t("series.ticket.waiting", { numbers: waiting.map((p) => `#${p.number}`).join(" + ") }), tone: "run" };
      return { text: t("series.ticket.done"), tone: "idle" };
    default:
      return ticket.status === null ? { text: t("series.ticket.notStarted"), tone: "idle" }
        : { text: t(ticket.status === "success" ? "series.ticket.done" : "series.ticket.ended"), tone: "idle" };
  }
}
