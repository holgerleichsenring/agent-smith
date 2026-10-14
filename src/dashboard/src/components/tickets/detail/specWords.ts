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

/** Verified criteria across every spec: the machine verdict — an operator judgement is a label beside it. */
export function acceptanceTally(series: TicketSeries | null): { verified: number; total: number } | null {
  if (!series?.recorded || series.specs.length === 0) return null;
  const done = series.specs.flatMap((s) => s.done);
  return { verified: done.filter(isVerified).length, total: done.length };
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
  const by = spec.executedByAttempt != null ? `A${spec.executedByAttempt}` : "";
  switch (criterion.verdict) {
    case "met": return { mark: "ok", glyph: "✓", who: by };
    case "unmet": return { mark: "bad", glyph: "✗", who: by };
    case "not_applicable": return { mark: "q", glyph: "–", who: by ? `${by} · not applicable` : "not applicable" };
    case "unproven": return { mark: "bad", glyph: "?", who: by ? `${by} · unproven` : "unproven" };
    default: return { mark: "q", glyph: "·", who: spec.state === "not_started" ? "not started" : "verify pending" };
  }
}

export function overruleWords(criterion: TicketSeriesCriterion): string | null {
  const o = criterion.overrule;
  return o ? `overruled: ${o.humanStatus.replace(/_/g, " ")} by ${o.author} — ${o.reason}` : null;
}
