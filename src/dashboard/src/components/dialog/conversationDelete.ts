import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-18-7a05: what a deletion does NOT undo, said before it happens. A deleted
// conversation is gone — there is no bin and no undo — so the only place this can be said is
// before the click is acted on.
//
// The wording is chosen by the KIND of the latest filing, because that is what decides whether
// anything in the tracker points back here. A filed phase and an epic parent carry a sentence
// naming the conversation; a bug ticket's body has none, and claiming one it never had would be
// a warning about nothing. The kind is nullable — it falls back to the latest proposal, which is
// cleared when a proposal is rejected or times out — so the third sentence is the one true of
// every filing, and of a conversation that filed nothing at all.

const NO_UNDO =
  "This cannot be undone: the conversation and every answer you gave in it are deleted.";

/**
 * The confirmation this conversation needs, or null when it needs none. A conversation with no
 * turns has nothing to lose — nothing was said in it and nothing could have been filed from it —
 * and is deleted without ceremony. Everything else is confirmed, including a conversation that
 * filed nothing: the filing marker is younger than the conversations that filed real tickets
 * before it existed, and confirming too much is the cheap side of that error.
 */
export function deletionWarning(conversation: SpecDialogSessionSummary): string | null {
  if (conversation.turns === 0) return null;
  return `Delete “${named(conversation)}”?\n\n${NO_UNDO}\n\n${survives(conversation)}`;
}

// 2026-09-21-f237a: the sentence the person wrote, and only where there is none the subject the
// model minted for it. A conversation opened with a pasted block has no first line at all, and
// asking about "untitled s-9" beside a row that reads its subject is worse than asking about the
// subject.
function named(conversation: SpecDialogSessionSummary): string {
  return conversation.title ?? conversation.subject ?? `untitled ${conversation.sessionId}`;
}

function survives(conversation: SpecDialogSessionSummary): string {
  const kind = conversation.outcome?.kind ?? null;
  if (kind === "phase" || kind === "epic") {
    return "The tickets it filed stay in the tracker, and the specification approved here stays"
      + " with them — but the sentence in the ticket that names this conversation will stop"
      + " resolving.";
  }
  if (kind === "bug") {
    return "The tickets it filed stay in the tracker, and the specification approved here stays"
      + " with them.";
  }
  return "Whatever it filed stays in the tracker, and the map from this conversation to it stops"
    + " resolving.";
}
