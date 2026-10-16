import { isRunLive } from "@/lib/runLiveness";
import type { Read } from "@/lib/tickets/TicketPageReads";
import type {
  RunPasses, TicketAttempt, TicketDetail, TicketSeries, TicketSeriesCriterion, TicketSeriesSpec,
} from "@/types/tickets";

// 2026-10-13-9a59r: the Specs and Acceptance tabs' words, from 9a59e's series (its per-spec state, done list
// and verdicts), 9a59f's steps as worked and passes, and 9a59d's revisions — nothing else.

export const SPEC_STATE: Record<string, [string, string]> = {
  done: ["ok", "done"],
  in_progress: ["amber", "working"],
  not_started: ["q", "not started"],
  failed: ["bad", "not done"],
  handed_back: ["person", "handed back"],
  carried: ["ok", "carried"],
};

export function specStatePill(state: string): [string, string] {
  return SPEC_STATE[state] ?? ["q", state.replace(/_/g, " ")];
}

/** The specs counter: "n · rev k" from the newest recorded revision number, n alone without one. */
export function specsCounter(detail: TicketDetail, series: TicketSeries | null): string | null {
  if (!series?.recorded) return null;
  const revisions = detail.attempts.map((a) => a.revision?.number ?? 0);
  const newest = Math.max(0, ...revisions);
  return newest > 0 ? `${series.specs.length} · rev ${newest}` : `${series.specs.length}`;
}

export function isVerified(criterion: TicketSeriesCriterion): boolean {
  return criterion.verdict === "met";
}

/** The line a run-record series is introduced by. */
export const RUN_RECORD_LINE = "as the runs recorded them — these attempts predate the cut";

/** A series read from the run record of attempts that predate the cut. */
export function isRunRecord(series: TicketSeries | null | undefined): boolean {
  return series?.source === "run-record";
}

/** Verified criteria across the specs whose verdicts were recorded: the machine verdict — an operator judgement
 *  is a label beside it — and how many specs recorded none. */
export function acceptanceTally(series: TicketSeries | null): { verified: number; total: number; unrecorded: number } | null {
  if (!series?.recorded || series.specs.length === 0) return null;
  const judged = series.specs.filter((s) => s.verdictRecorded !== false);
  const done = judged.flatMap((s) => s.done);
  return { verified: done.filter(isVerified).length, total: done.length, unrecorded: series.specs.length - judged.length };
}

export interface AttemptCell {
  kind: "none" | "done" | "carried" | "fail" | "live" | "recut";
  text: string;
  title: string;
}

/** One spec's cell for one attempt — that attempt's own cut (series?runId=) and passes. */
export function attemptCell(
  specId: string, attempt: TicketAttempt, cut: Read<TicketSeries> | undefined, passes: Read<RunPasses> | undefined,
): AttemptCell {
  const own = cut?.data?.runId === attempt.runId ? cut.data : null;
  const spec = own?.specs.find((s) => s.specId === specId);
  const label = `A${attempt.number}`;
  if (!spec) return { kind: "none", text: "—", title: `${label}: not run` };
  switch (spec.state) {
    case "carried": return { kind: "carried", text: "·", title: `${label}: carried` };
    case "done": {
      const n = passes?.data?.specs.find((p) => p.specId === specId)?.passes.length ?? 0;
      return { kind: "done", text: n > 0 ? String(n) : "✓", title: `${label}: done${n > 0 ? ` in ${n} pass${n === 1 ? "" : "es"}` : ""}` };
    }
    case "in_progress":
      return isRunLive(attempt.status)
        ? { kind: "live", text: "▶", title: `${label}: working` }
        : { kind: "fail", text: "✗", title: `${label}: failed` };
    case "not_started": return { kind: "recut", text: "○", title: `${label}: not started` };
    default: return { kind: "fail", text: "✗", title: `${label}: ${specStatePill(spec.state)[1]}` };
  }
}

/** The live spec first, else the first by ordinal. */
export function defaultSpec(specs: TicketSeriesSpec[]): TicketSeriesSpec | null {
  return specs.find((s) => s.state === "in_progress") ?? specs[0] ?? null;
}

/** A criterion's mark, words and the attempt that verified it. */
export function criterionRow(criterion: TicketSeriesCriterion, spec: TicketSeriesSpec): { mark: string; glyph: string; who: string } {
  const unjudged = spec.verdictRecorded === false ? "no verdict recorded"
    : spec.state === "not_started" ? "not started" : "verify pending";
  return verdictRow(criterion, spec.executedByAttempt != null ? `A${spec.executedByAttempt}` : "", unjudged);
}

/** A criterion's mark and words from its verdict, by the attempt named; `unjudged` when it has none. */
export function verdictRow(criterion: TicketSeriesCriterion, by: string, unjudged: string): { mark: string; glyph: string; who: string } {
  switch (criterion.verdict) {
    case "met": return { mark: "ok", glyph: "✓", who: by };
    case "unmet": return { mark: "bad", glyph: "✗", who: by };
    case "not_applicable": return { mark: "q", glyph: "–", who: by ? `${by} · not applicable` : "not applicable" };
    case "unproven": return { mark: "bad", glyph: "?", who: by ? `${by} · unproven` : "unproven" };
    default: return { mark: "q", glyph: "·", who: unjudged };
  }
}

const RECORD_NOTES: Record<string, string> = {
  "no record": "no spec record — only the goal was recorded",
  "record unreadable": "the spec record could not be read",
};

/** Why a spec shows only its goal; null when its record was read. */
export function recordNoteWords(spec: TicketSeriesSpec): string | null {
  if (spec.hasRecord !== false) return null;
  return RECORD_NOTES[spec.recordNote ?? ""] ?? RECORD_NOTES["no record"];
}

/** 2026-10-16-86e6e: why the acceptance gate did not accept a criterion — its note on an unmet or unproven verdict. */
export function rejectionOf(criterion: TicketSeriesCriterion): string | null {
  if (criterion.verdict !== "unmet" && criterion.verdict !== "unproven") return null;
  return criterion.note && criterion.note.trim() !== "" ? criterion.note : null;
}

export function overruleWords(criterion: TicketSeriesCriterion): string | null {
  const o = criterion.overrule;
  return o ? `overruled: ${o.humanStatus.replace(/_/g, " ")} by ${o.author} — ${o.reason}` : null;
}
