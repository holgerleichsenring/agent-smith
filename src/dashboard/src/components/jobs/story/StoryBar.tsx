"use client";

import { cn } from "@/lib/utils";
import type { Beat, BeatStatus } from "./beatMapping";

// p0344: the horizontal 5-beat storybar rendered ABOVE the master/detail trace.
// The story reads left→right: Ticket → Plan → Building → Verify → Outcome. Each
// beat carries a state derived from the steps folded into it; clicking a beat
// asks the page to focus the relevant part (select the beat's first step in the
// rail, or scroll the Verify panel into view).

const DOT_CLASS: Record<BeatStatus, string> = {
  done: "bg-emerald-500",
  active: "bg-amber-500 animate-pulse",
  fail: "bg-rose-500",
  idle: "bg-stone-300",
};

const RING_CLASS: Record<BeatStatus, string> = {
  done: "border-emerald-300 bg-emerald-50",
  active: "border-amber-300 bg-amber-50",
  fail: "border-rose-300 bg-rose-50",
  idle: "border-stone-200 bg-white",
};

const CAPTION: Record<BeatStatus, string> = {
  done: "done",
  active: "in progress",
  fail: "failed",
  idle: "pending",
};

interface StoryBarProps {
  beats: Beat[];
  onBeatClick?: (beat: Beat) => void;
}

export function StoryBar({ beats, onBeatClick }: StoryBarProps) {
  return (
    <div
      data-testid="story-bar"
      className="flex items-stretch gap-2 overflow-x-auto"
      role="list"
      aria-label="Run story beats"
    >
      {beats.map((beat, i) => (
        <BeatMarker
          key={beat.key}
          beat={beat}
          isLast={i === beats.length - 1}
          onClick={() => onBeatClick?.(beat)}
        />
      ))}
    </div>
  );
}

function BeatMarker({
  beat,
  isLast,
  onClick,
}: {
  beat: Beat;
  isLast: boolean;
  onClick: () => void;
}) {
  return (
    <div className="flex flex-1 items-center gap-2" role="listitem">
      <button
        type="button"
        data-testid={`story-beat-${beat.key}`}
        data-status={beat.status}
        onClick={onClick}
        className={cn(
          "flex w-full items-center gap-2.5 rounded-lg border px-3 py-2.5 text-left transition-colors hover:brightness-[0.98]",
          RING_CLASS[beat.status],
        )}
      >
        <span
          data-testid={`story-beat-${beat.key}-dot`}
          className={cn("h-2.5 w-2.5 flex-none rounded-full", DOT_CLASS[beat.status])}
          aria-hidden="true"
        />
        <span className="min-w-0">
          <span className="block truncate dsh-body font-medium text-stone-800">
            <span className="mr-1 font-mono dsh-label text-stone-400">{beat.index}</span>
            {beat.label}
          </span>
          <span
            data-testid={`story-beat-${beat.key}-caption`}
            className="block dsh-label text-stone-500"
          >
            {CAPTION[beat.status]}
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
