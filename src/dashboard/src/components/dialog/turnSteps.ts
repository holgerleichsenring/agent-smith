import type { SpecDialogActivityKind, SpecDialogActivityPush, SpecDialogActivityStep } from "@/types/spec-dialog";

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
 *  otherwise grow without limit; a server-side bound does not bound this array. 2026-11-09-9631c:
 *  it was 50, so a turn of 438 steps showed "47 earlier steps" and the row froze there while the
 *  count beside it ran on. A long review really reaches hundreds, so the bound sits above that. */
export const ACTIVITY_KEPT = 1000;

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

// 2026-11-19-687ec: a page that opens (or reopens) a running turn is served only the tail the server keeps in memory,
// and a reconnect loses the pushes of its gap. Every step is also kept on the turn's run, so the page reads the gap
// back from there instead of showing a list that starts in the middle.

/** Whether the held steps miss a sequence above `readUpTo` — the highest one a history read already returned. A gap
 *  at or below it is a step the run never kept (one reported before the run had an id), which no read will fill. A
 *  page holding its full ACTIVITY_KEPT keeps only the newest steps on purpose, so nothing is missing there. */
export function historyGap(held: SpecDialogActivityPush[], readUpTo: number): boolean {
  if (held.length >= ACTIVITY_KEPT) return false;
  let expected = 1;
  for (const step of held) {
    if (step.seq > expected && step.seq - 1 > readUpTo) return true;
    expected = Math.max(expected, step.seq + 1);
  }
  return false;
}

/** The steps a run kept for THIS turn, as the page holds them. The turn is compared as an instant and re-stamped with
 *  the held string, because the merge tells turns apart by that string. */
export function historyOf(
  kept: SpecDialogActivityStep[],
  dialogId: string,
  turnStartedAt: string,
): SpecDialogActivityPush[] {
  const turn = Date.parse(turnStartedAt);
  return kept
    .filter((step) => step.turnStartedAt != null && Date.parse(step.turnStartedAt) === turn)
    .map((step) => ({
      dialogId,
      kind: step.kind as SpecDialogActivityKind,
      name: step.name ?? null,
      detail: step.detail ?? null,
      at: step.timestamp,
      seq: step.seq,
      turnStartedAt,
    }));
}
