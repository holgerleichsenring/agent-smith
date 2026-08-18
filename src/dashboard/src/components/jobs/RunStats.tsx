"use client";

import type { ReactNode } from "react";
import type { BeatState, RunBeats, RunSnapshot } from "@/types/hub-events";
import { monotonizeBeats } from "@/lib/beatMonotonic";

// p0445: the right-hand columns of a run row — story spine · step position ·
// cost · elapsed. Extracted from RunRow so the parked-run card can state the
// same facts: a run waiting on an operator has a position, a price and an age
// exactly like a finished one, and withholding them made the single row that
// needs a decision the only one that could not be read at a glance.

const SPINE_ORDER: Array<keyof RunBeats> = ["ticket", "plan", "building", "verify", "outcome"];

const SPINE_CLASS: Record<BeatState, string> = {
  done: "d",
  active: "n",
  failed: "f",
  pending: "",
  skipped: "",
};

// The mini story spine — 5 dots, one per beat, ONLY from server-computed beats.
// p0355: clamp to a monotonic sequence so a dot can't read "done" ahead of an
// earlier still-running beat.
export function Spine({ beats }: { beats: RunBeats }) {
  const view = monotonizeBeats(beats);
  return (
    <div
      className="spine hidesm"
      title="ticket · plan · build · verify · outcome"
      data-testid="run-row-spine"
    >
      {SPINE_ORDER.map((key) => (
        <i key={key} className={SPINE_CLASS[view[key]] || undefined} data-beat={key} data-state={view[key]} />
      ))}
    </div>
  );
}

export function relativeAgo(iso: string): string {
  const then = new Date(iso).getTime();
  const seconds = Math.max(0, Math.round((Date.now() - then) / 1000));
  if (seconds < 45) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

export function duration(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - start) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${(minutes % 60).toString().padStart(2, "0")}m`;
}

interface StatsProps {
  snapshot: RunSnapshot;
  // What stands in the spine's place when the snapshot carries no beats — the
  // finished pill on a run row, nothing on the parked card.
  spineFallback?: ReactNode;
  progressTestId?: string;
}

export function RunStats({ snapshot, spineFallback, progressTestId }: StatsProps) {
  const cost = snapshot.costUsd > 0 ? `$${snapshot.costUsd.toFixed(2)}` : "";
  const position = snapshot.totalSteps > 0 ? `${snapshot.stepIndex}/${snapshot.totalSteps}` : "—";
  return (
    <>
      {snapshot.beats ? (
        <Spine beats={snapshot.beats} />
      ) : (
        spineFallback ?? <span className="spine hidesm" />
      )}
      <span className="prog hidesm" data-testid={progressTestId}>
        {position}
      </span>
      <span className="cost hidesm">{cost}</span>
      <span className="prog">{duration(snapshot.startedAt, snapshot.finishedAt)}</span>
    </>
  );
}
