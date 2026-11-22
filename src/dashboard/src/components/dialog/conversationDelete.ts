import type { T } from "@/lib/i18n/translate";
import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-18-7a05: what a deletion does NOT undo, said before it happens. A deleted
// conversation is gone — there is no bin and no undo — so the only place this can be said is
// before the click is acted on.
//
// The wording is chosen by the KIND of the latest filing, because that is what decides whether
// anything in the tracker points back here. A filed spec and an epic parent carry a sentence
// naming the conversation, and a list's tickets are told the same (2026-10-14-dbcek); a bug
// ticket's body has none, and claiming one it never had would be a warning about nothing. The kind is nullable — it falls back to the latest proposal, which is
// cleared when a proposal is rejected or times out — so the third sentence is the one true of
// every filing, and of a conversation that filed nothing at all.

/**
 * The confirmation this conversation needs, or null when it needs none. A conversation with no
 * turns has nothing to lose — nothing was said in it and nothing could have been filed from it —
 * and is deleted without ceremony. Everything else is confirmed, including a conversation that
 * filed nothing: the filing marker is younger than the conversations that filed real tickets
 * before it existed, and confirming too much is the cheap side of that error.
 */
export function deletionWarning(t: T, conversation: SpecDialogSessionSummary): string | null {
  if (conversation.turns === 0) return null;
  const question = t("dialog.delete.question", { name: named(t, conversation) });
  return `${question}\n\n${t("dialog.delete.noUndo")}\n\n${survives(t, conversation)}`;
}

// 2026-09-21-f237a: the sentence the person wrote, and only where there is none the subject the
// model minted for it. A conversation opened with a pasted block has no first line at all, and
// asking about "untitled s-9" beside a row that reads its subject is worse than asking about the
// subject.
function named(t: T, conversation: SpecDialogSessionSummary): string {
  return conversation.title ?? conversation.subject ?? t("dialog.conversations.untitled", { id: conversation.sessionId });
}

function survives(t: T, conversation: SpecDialogSessionSummary): string {
  const kind = conversation.outcome?.kind ?? null;
  if (kind === "spec" || kind === "epic" || kind === "tickets") return t("dialog.delete.survivesLinked");
  if (kind === "bug") return t("dialog.delete.survivesBug");
  return t("dialog.delete.survivesOther");
}
