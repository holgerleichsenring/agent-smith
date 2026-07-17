"use client";

import { cn } from "@/lib/utils";
import type { BeatState, RunBeats } from "@/types/hub-events";

// p0344b: the horizontal 5-beat storybar rendered ABOVE the stage. The story
// reads left→right: Ticket → Plan → Building → Verify → Outcome. Every state is
// SERVER-computed (snapshot.beats) — this component renders, it never derives.
// A run without beats renders no storybar at all (the parent decides).
// p0343c (pixel identity): emits the run-viewer.html .storybar/.beat DOM
// verbatim — .marker circle, .bt title, .bs sub, s-done/s-run/s-wait/s-fail/
// s-idle state classes, aria-current on the selected beat. Clicking a beat
// switches the stage panel (the parent owns the selection).

export type BeatKey = keyof RunBeats;

export const BEAT_ORDER: BeatKey[] = ["ticket", "plan", "building", "verify", "outcome"];

export const BEAT_LABELS: Record<BeatKey, string> = {
  ticket: "The ticket",
  plan: "The plan",
  building: "Building",
  verify: "Verify",
  outcome: "Outcome",
};

// BeatState → the mock's s-* class. `paused` (waiting_for_input) upgrades the
// active beat to the mock's s-wait look with the "?" marker.
function beatClass(state: BeatState, paused: boolean): string {
  switch (state) {
    case "done":
      return "s-done";
    case "active":
      return paused ? "s-wait" : "s-run";
    case "failed":
      return "s-fail";
    default:
      return "s-idle";
  }
}

function marker(state: BeatState, paused: boolean): string {
  if (state === "done") return "✓";
  if (state === "failed") return "✗";
  if (state === "active" && paused) return "?";
  return "";
}

interface StoryBarProps {
  beats: RunBeats;
  /** Real per-beat sub captions derived by the parent from snapshot data. */
  subs: Record<BeatKey, string>;
  /** The beat whose stage panel is showing (aria-current). */
  selected: BeatKey;
  /** True while the run is parked on an operator question (s-wait look). */
  paused?: boolean;
  onBeatClick?: (beat: BeatKey) => void;
}

export function StoryBar({ beats, subs, selected, paused = false, onBeatClick }: StoryBarProps) {
  return (
    <nav className="storybar" aria-label="Run story" data-testid="story-bar">
      {BEAT_ORDER.map((key) => {
        const state = beats[key];
        const isPaused = paused && state === "active";
        return (
          <button
            key={key}
            type="button"
            className={cn("beat", beatClass(state, isPaused))}
            data-beat={key}
            data-status={state}
            data-testid={`story-beat-${key}`}
            aria-current={selected === key ? "true" : "false"}
            onClick={() => onBeatClick?.(key)}
          >
            <div className="marker">{marker(state, isPaused)}</div>
            <div>
              <div className="bt">{BEAT_LABELS[key]}</div>
              <div className="bs" data-testid={`story-beat-${key}-caption`}>
                {subs[key]}
              </div>
            </div>
          </button>
        );
      })}
    </nav>
  );
}
