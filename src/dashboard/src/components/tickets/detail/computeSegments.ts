import type { RunCompute, RunComputeSegment, TicketAttempt } from "@/types/tickets";
import { attemptEnd, duration, PARK_HANDBACK, PARK_QUESTION } from "./computeTotals";

// 2026-10-13-9a59s: one attempt's wall clock as pieces on its own row — each launch segment as ONE span from
// its start to its end, and between launches what the run waited on: a person (a question or hand-back park,
// drawn without pods because a park releases them), capacity (a segment ended 'queued'), or nothing recorded.
// A segment's call time is summed call durations, which parallel sub-agents overlap, so it is written beside
// the bar as a figure and never drawn as a share of the span.

export type WallPieceKind = "segment" | "park" | "capacity" | "idle";

export interface WallPiece {
  kind: WallPieceKind;
  ms: number;
  /** Percent of this attempt's wall clock. */
  pct: number;
  title: string;
}

export interface SegmentFigure {
  launch: number;
  wallMs: number;
  callMs: number;
}

export interface AttemptWallClock {
  wallMs: number;
  pieces: WallPiece[];
  figures: SegmentFigure[];
}

function at(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? null : ms;
}

/** What the run waited on after a segment ended. */
function waitKind(ended: RunComputeSegment | null): WallPieceKind {
  if (!ended) return "idle";
  if (ended.endStatus === "queued") return "capacity";
  if (ended.endStatus === "waiting_for_input") return "park";
  return "idle";
}

const WAIT_TITLES: Record<WallPieceKind, string> = {
  segment: "running",
  park: "parked · no pods, no tokens",
  capacity: "waiting for capacity",
  idle: "not running",
};

function parkTitle(ended: RunComputeSegment | null): string {
  if (ended?.park?.kind === PARK_QUESTION) return "parked on a question · no pods, no tokens";
  if (ended?.park?.kind === PARK_HANDBACK) return "handed back · waiting for a reply";
  return WAIT_TITLES.park;
}

export function computeSegments(attempt: TicketAttempt, compute: RunCompute | null, now: number): AttemptWallClock {
  const start = at(attempt.startedAt) ?? now;
  const end = Math.max(start, attemptEnd(attempt, compute, now));
  const wallMs = end - start;
  const raw: Array<{ kind: WallPieceKind; ms: number; title: string }> = [];
  const figures: SegmentFigure[] = [];
  let cursor = start;
  let previous: RunComputeSegment | null = null;
  const gap = (until: number) => {
    if (until <= cursor) return;
    const kind = waitKind(previous);
    raw.push({ kind, ms: until - cursor, title: kind === "park" ? parkTitle(previous) : WAIT_TITLES[kind] });
    cursor = until;
  };
  (compute?.segments ?? []).forEach((segment, i) => {
    const from = Math.min(end, Math.max(cursor, at(segment.startedAt) ?? cursor));
    gap(from);
    const to = Math.min(end, Math.max(from, at(segment.endedAt) ?? end));
    raw.push({ kind: "segment", ms: to - from, title: `launch ${i + 1} · ${duration(to - from)} wall · ${duration(segment.modelMs)} call time` });
    figures.push({ launch: i + 1, wallMs: to - from, callMs: segment.modelMs });
    cursor = to;
    previous = segment;
  });
  gap(end);
  const pieces = raw.filter((p) => p.ms > 0).map((p) => ({ ...p, pct: wallMs > 0 ? (p.ms / wallMs) * 100 : 0 }));
  return { wallMs, pieces, figures };
}

/** The rows' common scale: the longest attempt's wall clock is the full width. */
export function wallScale(clocks: AttemptWallClock[]): (wallMs: number) => number {
  const longest = Math.max(0, ...clocks.map((c) => c.wallMs));
  return (wallMs) => (longest > 0 ? (wallMs / longest) * 100 : 0);
}
