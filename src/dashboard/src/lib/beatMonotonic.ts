import type { BeatState, RunBeats } from "@/types/hub-events";

// p0355: the 5-beat story spine is server-computed (snapshot.beats), but the
// projector has been observed emitting a NON-MONOTONIC set — e.g. Building
// "done" while The plan is still "active". A later beat cannot have completed
// before an earlier one; rendering that verbatim reads as a lie (green ✓ on
// Building while Plan spins amber). This is a pure display guard: walk the
// beats in story order and, once an earlier beat is not yet complete, no later
// beat may claim `done`/`active` — clamp it back to `pending`. A `failed` beat
// blocks the rest too (its own state is preserved). The frontend renders, it
// never invents state: this only ever DOWNGRADES an impossible-ahead beat.

export const BEAT_SEQUENCE: (keyof RunBeats)[] = [
  "ticket",
  "plan",
  "building",
  "verify",
  "outcome",
];

function isComplete(state: BeatState): boolean {
  return state === "done" || state === "skipped";
}

export function monotonizeBeats(beats: RunBeats): RunBeats {
  const out: RunBeats = { ...beats };
  let blocked = false; // set once an earlier beat has not completed
  for (const key of BEAT_SEQUENCE) {
    if (blocked && (out[key] === "done" || out[key] === "active")) {
      out[key] = "pending";
    }
    if (!isComplete(out[key])) blocked = true;
  }
  return out;
}
