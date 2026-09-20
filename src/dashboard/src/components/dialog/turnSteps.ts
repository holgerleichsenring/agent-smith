import type { SpecDialogActivityPush } from "@/types/spec-dialog";

// 2026-09-18-2f8b: the steps of ONE running turn, as the page holds them. They reach it two
// ways — served by the conversation read and pushed over the hub — and the two overlap, so
// they are merged rather than replaced. The merge key is (turn, sequence):
//
//   THE SEQUENCE, because a moment cannot tell two identical tool calls apart and matching on
//   the values would collapse them into one, corrupting the step count a reader watches.
//
//   THE TURN, because the sequence restarts with every turn. The page's only other defence
//   was the reply push that clears the list, and a reconnect loses every push sent during the
//   gap — so the next turn's steps would meet a stale list and be filtered out as duplicates.

/** How many steps of one turn the page keeps. A long turn in a tab someone leaves open would
 *  otherwise grow without limit; a server-side bound does not bound this array. */
export const ACTIVITY_KEPT = 50;

/** The turn the held steps belong to, or null when none are held. */
function heldTurn(held: SpecDialogActivityPush[]): string | null {
  return held.length > 0 ? held[0].turnStartedAt : null;
}

function isNewer(turn: string, than: string): boolean {
  return Date.parse(turn) > Date.parse(than);
}

/** Drops what is held when `turn` names a LATER turn than the one it belongs to. A read may
 *  carry a new turn with no steps yet, and the old turn's steps are not that turn's. */
export function ofTurn(
  held: SpecDialogActivityPush[],
  turn: string | null,
): SpecDialogActivityPush[] {
  const holding = heldTurn(held);
  if (turn === null || holding === null || turn === holding) return held;
  return isNewer(turn, holding) ? [] : held;
}

/** Folds arriving steps into the held ones. A step of a later turn replaces what is held; one
 *  of an earlier turn is ignored; one of the same turn joins unless its sequence is there. */
export function mergedSteps(
  held: SpecDialogActivityPush[],
  arriving: SpecDialogActivityPush[],
): SpecDialogActivityPush[] {
  let kept = held;
  for (const step of arriving) {
    const holding = heldTurn(kept);
    if (holding !== null && holding !== step.turnStartedAt) {
      if (!isNewer(step.turnStartedAt, holding)) continue;
      kept = [];
    }
    if (kept.some((known) => known.seq === step.seq)) continue;
    kept = [...kept, step].sort((a, b) => a.seq - b.seq).slice(-ACTIVITY_KEPT);
  }
  return kept;
}

/** How many steps the TURN has taken, which is not how many this page still holds: the list
 *  above is bounded, so past that bound a count of it freezes and one of the two liveness
 *  cues dies for the rest of the turn. The sequence is the true count. */
export function stepCount(held: SpecDialogActivityPush[]): number {
  return held.reduce((highest, step) => Math.max(highest, step.seq), 0);
}
