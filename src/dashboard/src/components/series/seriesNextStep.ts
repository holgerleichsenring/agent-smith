import type { T } from "@/lib/i18n/translate";
import type { SeriesRunRow } from "@/types/seriesRuns";

// 2026-11-23-31dcc: the one sentence a series row says about what comes next, from that row alone — an ended series
// says so; otherwise a question for a person first, then the merge that is ready, then the held tickets (2026-11-23-31dcg: the
// series' held count), else nothing to do.

export function seriesNextStep(row: SeriesRunRow, t: T): string {
  if (row.state === "done") return t("series.next.done");
  if (row.state === "stopped") return t("series.next.stopped");
  if (row.question) return t("series.next.question");
  if (row.nextMerge) return t("series.next.merge", { key: row.nextMerge });
  if (row.counts.held > 0) return t("series.next.held", { count: row.counts.held });
  return t("series.next.working");
}

/** 2026-11-26-5201d: a series that will not move again — done, or stopped by a person. Its record stays (a stopped
 *  series of one is what keeps its ticket from being reopened); the list only hides it until asked. */
export function seriesEnded(state: string): boolean {
  return state === "done" || state === "stopped";
}

/** The series' state word; an unknown state reads as it came. */
export function seriesStateWord(state: string, t: T): string {
  switch (state) {
    case "running": return t("series.state.running");
    case "verifying": return t("series.state.verifying");
    case "delivering": return t("series.state.delivering"); // 2026-11-24-f9c5e
    case "paused": return t("series.state.paused");
    case "done": return t("series.state.done");
    case "stopped": return t("series.state.stopped");
    default: return state;
  }
}
