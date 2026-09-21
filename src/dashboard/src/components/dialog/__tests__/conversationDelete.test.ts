import { describe, it, expect } from "vitest";

import { deletionWarning } from "@/components/dialog/conversationDelete";
import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-20-4b0ab: the rule that decides WHETHER a deletion is confirmed and WHAT it says had
// no test of its own. Its only coverage was seven window.confirm spies on the surface, three of
// which were the only assertions of the wording anywhere — and this phase removes all seven,
// because the asking moved into a dialog the page draws. The rule is a pure function of a
// conversation summary and is pinned here as one, so the wording survives the next change to
// HOW the asking is done.

function conversation(overrides: Partial<SpecDialogSessionSummary> = {}): SpecDialogSessionSummary {
  return {
    sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: "2026-09-15T09:00:00Z",
    title: "a widget that reads the ledger", outcome: null, openDialogId: null,
    ...overrides,
  };
}

describe("The deletion warning", () => {
  // A filed phase — and an epic parent — carries a sentence in the tracker that names this
  // conversation, so the warning says that sentence stops resolving.
  it("DeletionWarning_APhaseFiling_SaysWhatSurvivesInTheTracker", () => {
    const said = deletionWarning(
      conversation({ outcome: { kind: "phase", tickets: 1, partial: false } }));

    expect(said).not.toBeNull();
    expect(said!).toContain("Delete “a widget that reads the ledger”?");
    expect(said!).toContain("This cannot be undone");
    expect(said!).toContain("The tickets it filed stay in the tracker");
    expect(said!).toContain("the specification approved here stays with them");
    expect(said!).toContain(
      "the sentence in the ticket that names this conversation will stop resolving");

    // The epic parent carries the same sentence, for the same reason.
    expect(deletionWarning(conversation({ outcome: { kind: "epic", tickets: 3, partial: false } })))
      .toBe(said);
  });

  // A bug ticket's body has no such sentence, and warning about one it never had would be a
  // warning about nothing.
  it("DeletionWarning_ABugFiling_SaysItsOwnSentence", () => {
    const said = deletionWarning(
      conversation({ outcome: { kind: "bug", tickets: 1, partial: false } }));

    expect(said).not.toBeNull();
    expect(said!).toContain("The tickets it filed stay in the tracker");
    expect(said!).not.toContain("names this conversation");
    expect(said!).not.toContain("stop resolving");
  });

  // The kind is nullable — it falls back to the latest proposal, which a rejection clears — so
  // a conversation that filed nothing at all is still confirmed, in the sentence true of every
  // filing. Confirming too much is the cheap side of that error.
  it("DeletionWarning_AConversationThatFiledNothing_StillWarns", () => {
    const filedNothing = deletionWarning(conversation({ outcome: null }));
    const noKind = deletionWarning(
      conversation({ outcome: { kind: null, tickets: 2, partial: false } }));

    expect(filedNothing).toContain(
      "Whatever it filed stays in the tracker, and the map from this conversation to it stops"
      + " resolving.");
    expect(noKind).toBe(filedNothing);

    // A conversation nobody titled is named by its session id rather than by an empty quote.
    expect(deletionWarning(conversation({ title: null })))
      .toContain("Delete “untitled s-9”?");
  });

  // Nothing was said in it and nothing could have been filed from it.
  it("DeletionWarning_AConversationWithNoTurns_NeedsNone", () => {
    expect(deletionWarning(conversation({ turns: 0 }))).toBeNull();
    // One turn is enough to be worth confirming.
    expect(deletionWarning(conversation({ turns: 1 }))).not.toBeNull();
  });
});
