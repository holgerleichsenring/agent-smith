import type { T, TextKey } from "@/lib/i18n/translate";
import { isRunLive } from "@/lib/runLiveness";
import type { Read } from "@/lib/tickets/TicketPageReads";
import type {
  RunPasses, TicketAttempt, TicketDetail, TicketSeries, TicketSeriesCriterion, TicketSeriesSpec,
} from "@/types/tickets";

// 2026-10-13-9a59r: the Specs and Acceptance tabs' words, from 9a59e's series (its per-spec state, done list
// and verdicts), 9a59f's steps as worked and passes, and 9a59d's revisions — nothing else.

export const SPEC_STATE: Record<string, [string, TextKey]> = {
  done: ["ok", "tickets.spec.state.done"],
  in_progress: ["amber", "tickets.spec.state.in_progress"],
  not_started: ["q", "tickets.spec.state.not_started"],
  failed: ["bad", "tickets.spec.state.failed"],
  handed_back: ["person", "tickets.spec.state.handed_back"],
  // 2026-11-27-3532d: retired undelivered; the spec that supersedes it is built in its place.
  superseded: ["q", "tickets.spec.state.superseded"],
  carried: ["ok", "tickets.spec.state.carried"],
};

export function specStatePill(t: T, state: string): [string, string] {
  const pill = SPEC_STATE[state];
  return pill ? [pill[0], t(pill[1])] : ["q", state.replace(/_/g, " ")];
}

/** The specs counter: "n · rev k" from the newest recorded revision number, n alone without one. */
export function specsCounter(t: T, detail: TicketDetail, series: TicketSeries | null): string | null {
  if (!series?.recorded) return null;
  const revisions = detail.attempts.map((a) => a.revision?.number ?? 0);
  const newest = Math.max(0, ...revisions);
  return newest > 0 ? t("tickets.specs.counterRev", { count: series.specs.length, rev: newest }) : `${series.specs.length}`;
}

export function isVerified(criterion: TicketSeriesCriterion): boolean {
  return criterion.verdict === "met";
}

/** The line a run-record series is introduced by: its dictionary key. */
export const RUN_RECORD_LINE: TextKey = "tickets.specs.runRecord";

/** A series read from the run record of attempts that predate the cut. */
export function isRunRecord(series: TicketSeries | null | undefined): boolean {
  return series?.source === "run-record";
}

/** Verified criteria across the specs whose verdicts were recorded: the machine verdict — an operator judgement
 *  is a label beside it — and how many specs recorded none. */
export function acceptanceTally(series: TicketSeries | null): { verified: number; total: number; unrecorded: number } | null {
  if (!series?.recorded || series.specs.length === 0) return null;
  // 2026-11-18-98fa: a spec its master reported on judged no line, so its lines are neither verified nor missing.
  const judged = series.specs.filter((s) => s.verdictRecorded !== false && !s.report);
  const done = judged.flatMap((s) => s.done);
  const reported = series.specs.filter((s) => s.report).length;
  return { verified: done.filter(isVerified).length, total: done.length, unrecorded: series.specs.length - judged.length - reported };
}

export interface AttemptCell {
  kind: "none" | "done" | "carried" | "fail" | "live" | "recut";
  text: string;
  title: string;
}

/** One spec's cell for one attempt — that attempt's own cut (series?runId=) and passes. */
export function attemptCell(
  t: T, specId: string, attempt: TicketAttempt, cut: Read<TicketSeries> | undefined, passes: Read<RunPasses> | undefined,
): AttemptCell {
  const own = cut?.data?.runId === attempt.runId ? cut.data : null;
  const spec = own?.specs.find((s) => s.specId === specId);
  const label = `A${attempt.number}`;
  const title = (state: string) => t("tickets.specs.cell", { label, state });
  if (!spec) return { kind: "none", text: "—", title: title(t("tickets.specs.cell.notRun")) };
  switch (spec.state) {
    case "carried": return { kind: "carried", text: "·", title: title(t("tickets.spec.state.carried")) };
    case "done": {
      const n = passes?.data?.specs.find((p) => p.specId === specId)?.passes.length ?? 0;
      return { kind: "done", text: n > 0 ? String(n) : "✓",
        title: title(n > 0 ? t("tickets.specs.cell.doneIn", { count: n }) : t("tickets.spec.state.done")) };
    }
    case "in_progress":
      return isRunLive(attempt.status)
        ? { kind: "live", text: "▶", title: title(t("tickets.spec.state.in_progress")) }
        : { kind: "fail", text: "✗", title: title(t("tickets.specs.cell.failed")) };
    case "not_started": return { kind: "recut", text: "○", title: title(t("tickets.spec.state.not_started")) };
    default: return { kind: "fail", text: "✗", title: title(specStatePill(t, spec.state)[1]) };
  }
}

/** The live spec first, else the first by ordinal. */
export function defaultSpec(specs: TicketSeriesSpec[]): TicketSeriesSpec | null {
  return specs.find((s) => s.state === "in_progress") ?? specs[0] ?? null;
}

/** A criterion's mark, words and the attempt that verified it. */
export function criterionRow(t: T, criterion: TicketSeriesCriterion, spec: TicketSeriesSpec): { mark: string; glyph: string; who: string } {
  // 2026-11-19-1b72c: a spec that recorded no verdict leaves the line unmarked rather than saying so.
  const unjudged = spec.report ? t("tickets.spec.seeReport") : spec.verdictRecorded === false ? ""
    : spec.state === "not_started" ? t("tickets.spec.state.not_started") : t("tickets.spec.verifyPending");
  return verdictRow(t, criterion, spec.executedByAttempt != null ? `A${spec.executedByAttempt}` : "", unjudged);
}

/** A criterion's mark and words from its verdict, by the attempt named; `unjudged` when it has none. */
export function verdictRow(t: T, criterion: TicketSeriesCriterion, by: string, unjudged: string): { mark: string; glyph: string; who: string } {
  const said = (key: TextKey) => (by ? `${by} · ${t(key)}` : t(key));
  switch (criterion.verdict) {
    case "met": return { mark: "ok", glyph: "✓", who: by };
    case "unmet": return { mark: "bad", glyph: "✗", who: by };
    case "not_applicable": return { mark: "q", glyph: "–", who: said("tickets.spec.notApplicable") };
    case "unproven": return { mark: "bad", glyph: "?", who: said("tickets.spec.unproven") };
    default: return { mark: "q", glyph: "·", who: unjudged };
  }
}

const RECORD_NOTES: Record<string, TextKey> = {
  "no record": "tickets.spec.noRecord",
  "record unreadable": "tickets.spec.recordUnreadable",
};

/** Why a spec shows only its goal; null when its record was read. */
export function recordNoteWords(t: T, spec: TicketSeriesSpec): string | null {
  if (spec.hasRecord !== false) return null;
  return t(RECORD_NOTES[spec.recordNote ?? ""] ?? RECORD_NOTES["no record"]);
}

/** 2026-10-16-86e6e: why the acceptance gate did not accept a criterion — its note on an unmet or unproven verdict. */
export function rejectionOf(criterion: TicketSeriesCriterion): string | null {
  if (criterion.verdict !== "unmet" && criterion.verdict !== "unproven") return null;
  return criterion.note && criterion.note.trim() !== "" ? criterion.note : null;
}

export function overruleWords(t: T, criterion: TicketSeriesCriterion): string | null {
  const o = criterion.overrule;
  return o ? t("tickets.spec.overruled", { status: humanStatusWords(t, o.humanStatus), author: o.author, reason: o.reason }) : null;
}

const HUMAN_STATUS: Record<string, TextKey> = {
  met: "tickets.spec.human.met", unmet: "tickets.spec.human.unmet", not_applicable: "tickets.spec.human.not_applicable",
  unproven: "tickets.spec.human.unproven",
};

/** A person's judgement of a criterion, in words: "met", "not applicable". */
export function humanStatusWords(t: T, status: string): string {
  return HUMAN_STATUS[status] ? t(HUMAN_STATUS[status]) : status.replace(/_/g, " ");
}
