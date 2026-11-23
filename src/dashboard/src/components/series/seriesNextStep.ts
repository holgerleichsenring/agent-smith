import type { T } from "@/lib/i18n/translate";
import type { SeriesRunRow } from "@/types/seriesRuns";

// 2026-11-23-31dcc: the one sentence a series row says about what comes next, from that row alone — an ended series
// says so; otherwise a question for a person first, then the merge that is ready, then stuck tickets, else nothing to do.

export function seriesNextStep(row: SeriesRunRow, t: T): string {
  if (row.state === "done") return t("series.next.done");
  if (row.state === "stopped") return t("series.next.stopped");
  if (row.question) return t("series.next.question");
  if (row.nextMerge) return t("series.next.merge", { key: row.nextMerge });
  if (row.counts.held > 0) return t("series.next.held", { count: row.counts.held });
  return t("series.next.working");
}

/** The series' state word; an unknown state reads as it came. */
export function seriesStateWord(state: string, t: T): string {
  switch (state) {
    case "running": return t("series.state.running");
    case "verifying": return t("series.state.verifying");
    case "paused": return t("series.state.paused");
    case "done": return t("series.state.done");
    case "stopped": return t("series.state.stopped");
    default: return state;
  }
}
