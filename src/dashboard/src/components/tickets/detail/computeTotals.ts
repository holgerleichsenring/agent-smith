import { isRunLive } from "@/lib/runLiveness";
import type { RunSnapshot } from "@/types/hub-events";
import type { RunCompute, RunComputeSegment, RunComputeSpawn, TicketAttempt } from "@/types/tickets";

// 2026-10-13-9a59s: the Compute tiles, from 9a59i's spawns, 9a59j's segments and parks and the run detail's
// reserved Gi-minutes — every figure is a sum of recorded instants, nothing estimated. Two rules decide what
// a figure means: an unobserved pod (no close event, disposedAt null) is clipped at the end of the segment it
// was spawned in, where the run detail charges it to run end — both are kept so the tile can name both; and
// only a question or hand-back park waits on a person — a segment ended 'queued' waits on capacity.

export const PARK_QUESTION = "question";
export const PARK_HANDBACK = "handback";
const QUEUED = "queued";

/** One attempt with the per-run reads that have landed (null while unread or unreadable). */
export interface AttemptMachine {
  attempt: TicketAttempt;
  compute: RunCompute | null;
  run: RunSnapshot | null;
  /** The attempt after this one, by run id — its cause answered this attempt's hand-back. */
  next: TicketAttempt | null;
}

export interface PodSpan {
  repo: string;
  from: number;
  to: number;
  /** The run detail's end for the same pod: disposedAt, else the run's end. */
  toRunEnd: number;
  endReason: string | null;
  /** No close event reached it: the span ends where its segment ended. */
  clipped: boolean;
}

export interface PersonWait {
  kind: "question" | "handback";
  attempt: number;
  runId: string;
  from: number;
  to: number;
  /** When the run stopped and released its sandboxes: the parked segment's end. */
  parkedFrom: number;
  /** Still waiting now. */
  open: boolean;
}

export interface ComputeTotals {
  podMinutes: number;
  /** The same pods with an unobserved one charged to run end, as the run detail does. */
  podMinutesToRunEnd: number;
  unobserved: number;
  /** Summed reserved Gi-minutes of terminal attempts, in Gi·h; null when none reports one. */
  reservedGiHours: number | null;
  modelMs: number;
  waits: PersonWait[];
  waitingMs: number;
  capacityMs: number;
  /** A pod's lifetime overlapped a question or hand-back park. */
  podsHeldWhileWaiting: boolean;
}

function at(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? null : ms;
}

/** Where an attempt's clock stops: its finish, now while it is live, else its last recorded instant. */
export function attemptEnd(attempt: TicketAttempt, compute: RunCompute | null, now: number): number {
  const finished = at(attempt.finishedAt);
  if (finished !== null) return finished;
  if (isRunLive(attempt.status)) return now;
  const last = compute?.segments[compute.segments.length - 1];
  return at(last?.endedAt) ?? at(last?.startedAt) ?? at(attempt.startedAt) ?? now;
}

function segmentOf(segments: RunComputeSegment[], spawnedAt: number): RunComputeSegment | null {
  let found: RunComputeSegment | null = null;
  for (const segment of segments) if ((at(segment.startedAt) ?? Infinity) <= spawnedAt) found = segment;
  return found;
}

/** Each spawn's lifetime; a spawn with no spawn time is left out rather than guessed. */
export function podSpans(attempt: TicketAttempt, compute: RunCompute | null, now: number): PodSpan[] {
  if (!compute) return [];
  const end = attemptEnd(attempt, compute, now);
  return compute.spawns.flatMap((spawn: RunComputeSpawn) => {
    const from = at(spawn.spawnedAt);
    if (from === null) return [];
    const disposed = at(spawn.disposedAt);
    const segment = disposed === null ? segmentOf(compute.segments, from) : null;
    const to = disposed ?? (isRunLive(attempt.status) && !spawn.endReason ? now : at(segment?.endedAt) ?? end);
    return [{
      repo: spawn.repo, from, to: Math.max(from, to), toRunEnd: Math.max(from, disposed ?? end),
      endReason: spawn.endReason, clipped: disposed === null && spawn.endReason !== null,
    }];
  });
}

/** The question and hand-back parks of one attempt, each from its asking to its answer. */
export function personWaits(machine: AttemptMachine, now: number): PersonWait[] {
  const { attempt, compute, next } = machine;
  const live = isRunLive(attempt.status);
  return (compute?.segments ?? []).flatMap((segment) => {
    const park = segment.park;
    if (!park || (park.kind !== PARK_QUESTION && park.kind !== PARK_HANDBACK)) return [];
    const from = at(park.askedAt);
    if (from === null) return [];
    const answered = park.kind === PARK_HANDBACK
      ? at(park.answeredAt) ?? at(next?.cause?.at)
      : at(park.answeredAt) ?? at(park.resumedAt);
    const open = answered === null && (live || park.kind === PARK_HANDBACK);
    if (answered === null && !open) return [];
    const to = Math.max(from, answered ?? now);
    return [{
      kind: park.kind, attempt: attempt.number, runId: attempt.runId, from, to, open,
      parkedFrom: Math.min(to, Math.max(from, at(segment.endedAt) ?? from)),
    }];
  });
}

/** The time segments ended 'queued' waited for capacity before the next launch (or until now). */
export function capacityMs(machine: AttemptMachine, now: number): number {
  const segments = machine.compute?.segments ?? [];
  const end = attemptEnd(machine.attempt, machine.compute, now);
  return segments.reduce((sum, segment, i) => {
    if (segment.endStatus !== QUEUED) return sum;
    const from = at(segment.endedAt);
    const to = i + 1 < segments.length ? at(segments[i + 1].startedAt) : end;
    return from === null || to === null ? sum : sum + Math.max(0, to - from);
  }, 0);
}

/** A pod was held across a park when it lived on after its run had parked. */
const overlaps = (pod: PodSpan, wait: PersonWait) => pod.from < wait.to && wait.parkedFrom < pod.to;

export function computeTotals(machines: AttemptMachine[], now: number): ComputeTotals {
  let podMs = 0, toRunEndMs = 0, unobserved = 0, modelMs = 0, capacity = 0, giMinutes = 0;
  let reserved = false, held = false;
  const waits: PersonWait[] = [];
  for (const machine of machines) {
    const pods = podSpans(machine.attempt, machine.compute, now);
    const mine = personWaits(machine, now);
    for (const pod of pods) {
      podMs += pod.to - pod.from;
      toRunEndMs += pod.toRunEnd - pod.from;
      if (pod.clipped) unobserved += 1;
      if (mine.some((wait) => overlaps(pod, wait))) held = true;
    }
    waits.push(...mine);
    modelMs += (machine.compute?.segments ?? []).reduce((sum, s) => sum + s.modelMs, 0);
    capacity += capacityMs(machine, now);
    const gi = machine.run?.reservedGiMinutes;
    if (!isRunLive(machine.attempt.status) && typeof gi === "number") {
      giMinutes += gi;
      reserved = true;
    }
  }
  return {
    podMinutes: podMs / 60000,
    podMinutesToRunEnd: toRunEndMs / 60000,
    unobserved,
    reservedGiHours: reserved ? giMinutes / 60 : null,
    modelMs,
    waits,
    waitingMs: waits.reduce((sum, w) => sum + (w.to - w.from), 0),
    capacityMs: capacity,
    podsHeldWhileWaiting: held,
  };
}

/** "45s", "6m", "1h 34m". */
export function duration(ms: number): string {
  const seconds = Math.max(0, Math.round(ms / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

const END_WORDS: Record<string, string> = {
  exited: "exited",
  "oom-killed": "out of memory",
  vanished: "vanished",
  gone: "gone before its end was read",
  "released-at-park": "released while waiting on you",
  "released-at-requeue": "released while waiting for capacity",
  "released-at-end": "released at run end",
  unobserved: "no close observed",
};

/** A pod's end in words; null (no end yet) reads "running" on a live attempt. */
export function podEndWords(endReason: string | null, live: boolean): string {
  if (!endReason) return live ? "running" : "no end recorded";
  return END_WORDS[endReason] ?? endReason;
}
