"use client";

import { cn } from "@/lib/utils";
import type { BeatState, RunBeats } from "@/types/hub-events";

// p0344b: the horizontal 5-beat storybar rendered ABOVE the master/detail
// trace. The story reads left→right: Ticket → Plan → Building → Verify →
// Outcome. Every state is SERVER-computed (snapshot.beats, derived from the
// typed pipeline commands) — this component renders, it never derives. A run
// without beats renders no storybar at all (the parent decides).

export type BeatKey = keyof RunBeats;

export const BEAT_ORDER: BeatKey[] = ["ticket", "plan", "building", "verify", "outcome"];

export const BEAT_LABELS: Record<BeatKey, string> = {
  ticket: "Ticket",
  plan: "Plan",
  building: "Building",
  verify: "Verify",
  outcome: "Outcome",
};

const DOT_CLASS: Record<BeatState, string> = {
  done: "bg-emerald-500",
  active: "bg-amber-500 animate-pulse",
  failed: "bg-rose-500",
  pending: "bg-stone-300",
  skipped: "bg-stone-200",
};

const RING_CLASS: Record<BeatState, string> = {
  done: "border-emerald-300 bg-emerald-50",
  active: "border-amber-300 bg-amber-50",
  failed: "border-rose-300 bg-rose-50",
  pending: "border-stone-200 bg-white",
  skipped: "border-stone-200 border-dashed bg-stone-50",
};

const CAPTION: Record<BeatState, string> = {
  done: "done",
  active: "in progress",
  failed: "failed",
  pending: "pending",
  skipped: "skipped",
};

interface StoryBarProps {
  beats: RunBeats;
  onBeatClick?: (beat: BeatKey) => void;
}

export function StoryBar({ beats, onBeatClick }: StoryBarProps) {
  return (
    <div
      data-testid="story-bar"
      className="flex items-stretch gap-2 overflow-x-auto"
      role="list"
      aria-label="Run story beats"
    >
      {BEAT_ORDER.map((key, i) => (
        <BeatMarker
          key={key}
          beatKey={key}
          state={beats[key]}
          index={i + 1}
          isLast={i === BEAT_ORDER.length - 1}
          onClick={() => onBeatClick?.(key)}
        />
      ))}
    </div>
  );
}

function BeatMarker({
  beatKey,
  state,
  index,
  isLast,
  onClick,
}: {
  beatKey: BeatKey;
  state: BeatState;
  index: number;
  isLast: boolean;
  onClick: () => void;
}) {
  const muted = state === "skipped";
  return (
    <div className="flex flex-1 items-center gap-2" role="listitem">
      <button
        type="button"
        data-testid={`story-beat-${beatKey}`}
        data-status={state}
        onClick={onClick}
        className={cn(
          "flex w-full items-center gap-2.5 rounded-lg border px-3 py-2.5 text-left transition-colors hover:brightness-[0.98]",
          RING_CLASS[state],
        )}
      >
        <span
          data-testid={`story-beat-${beatKey}-dot`}
          className={cn("h-2.5 w-2.5 flex-none rounded-full", DOT_CLASS[state])}
          aria-hidden="true"
        />
        <span className="min-w-0">
          <span
            className={cn(
              "block truncate dsh-body font-medium",
              muted ? "text-stone-400" : "text-stone-800",
            )}
          >
            <span className="mr-1 font-mono dsh-label text-stone-400">{index}</span>
            {BEAT_LABELS[beatKey]}
          </span>
          <span
            data-testid={`story-beat-${beatKey}-caption`}
            className={cn("block dsh-label", muted ? "text-stone-400" : "text-stone-500")}
          >
            {CAPTION[state]}
          </span>
        </span>
      </button>
      {!isLast && (
        <span aria-hidden="true" className="flex-none dsh-label text-stone-300">
          →
        </span>
      )}
    </div>
  );
}
