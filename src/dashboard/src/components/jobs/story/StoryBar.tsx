"use client";

import { cn } from "@/lib/utils";
import type { BeatState, RunBeats } from "@/types/hub-events";

// p0344b: the horizontal 5-beat storybar rendered ABOVE the master/detail
// trace. The story reads left→right: Ticket → Plan → Building → Verify →
// Outcome. Every state is SERVER-computed (snapshot.beats, derived from the
// typed pipeline commands) — this component renders, it never derives. A run
// without beats renders no storybar at all (the parent decides).
// p0343b (mock fidelity): beats render as NUMBERED CARDS in a row — done gets
// a green check + green tint border, active an amber tint card, failed rose,
// pending neutral, skipped muted dashed. The active beat's caption can carry
// the run's real step progress ("3 of 7") supplied by the parent.

export type BeatKey = keyof RunBeats;

export const BEAT_ORDER: BeatKey[] = ["ticket", "plan", "building", "verify", "outcome"];

export const BEAT_LABELS: Record<BeatKey, string> = {
  ticket: "Ticket",
  plan: "Plan",
  building: "Building",
  verify: "Verify",
  outcome: "Outcome",
};

const CARD_CLASS: Record<BeatState, string> = {
  done: "border-emerald-300 bg-emerald-50",
  active: "border-amber-300 bg-amber-50",
  failed: "border-rose-300 bg-rose-50",
  pending: "border-stone-200 bg-white",
  skipped: "border-stone-200 border-dashed bg-stone-50",
};

const MARKER_CLASS: Record<BeatState, string> = {
  done: "border-emerald-300 bg-emerald-100 text-emerald-700",
  active: "border-amber-300 bg-amber-100 text-amber-700",
  failed: "border-rose-300 bg-rose-100 text-rose-700",
  pending: "border-stone-200 bg-stone-100 text-stone-500",
  skipped: "border-stone-200 border-dashed bg-stone-50 text-stone-400",
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
  /** Real step progress ("3 of 7") rendered as the ACTIVE beat's caption —
   *  the parent derives it from snapshot.stepIndex/totalSteps; null keeps the
   *  plain "in progress" caption. */
  activeCaption?: string | null;
  onBeatClick?: (beat: BeatKey) => void;
}

export function StoryBar({ beats, activeCaption, onBeatClick }: StoryBarProps) {
  return (
    <div
      data-testid="story-bar"
      className="flex items-stretch gap-2 overflow-x-auto"
      role="list"
      aria-label="Run story beats"
    >
      {BEAT_ORDER.map((key, i) => (
        <BeatCard
          key={key}
          beatKey={key}
          state={beats[key]}
          index={i + 1}
          activeCaption={activeCaption ?? null}
          onClick={() => onBeatClick?.(key)}
        />
      ))}
    </div>
  );
}

function BeatCard({
  beatKey,
  state,
  index,
  activeCaption,
  onClick,
}: {
  beatKey: BeatKey;
  state: BeatState;
  index: number;
  activeCaption: string | null;
  onClick: () => void;
}) {
  const muted = state === "skipped";
  const caption = state === "active" && activeCaption ? activeCaption : CAPTION[state];
  return (
    <button
      type="button"
      role="listitem"
      data-testid={`story-beat-${beatKey}`}
      data-status={state}
      onClick={onClick}
      className={cn(
        "flex flex-1 items-center gap-2.5 rounded-lg border px-3 py-2.5 text-left transition-colors hover:brightness-[0.98]",
        CARD_CLASS[state],
      )}
    >
      <span
        data-testid={`story-beat-${beatKey}-marker`}
        aria-hidden="true"
        className={cn(
          "flex h-6 w-6 flex-none items-center justify-center rounded-full border font-mono dsh-label font-semibold",
          MARKER_CLASS[state],
        )}
      >
        {state === "done" ? "✓" : index}
      </span>
      <span className="min-w-0">
        <span
          className={cn(
            "block truncate dsh-body font-medium",
            muted ? "text-stone-400" : "text-stone-800",
          )}
        >
          {BEAT_LABELS[beatKey]}
        </span>
        <span
          data-testid={`story-beat-${beatKey}-caption`}
          className={cn("block dsh-label", muted ? "text-stone-400" : "text-stone-500")}
        >
          {caption}
        </span>
      </span>
    </button>
  );
}
