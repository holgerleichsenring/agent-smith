import { describe, expect, it } from "vitest";
import { trailingCard, transcriptEntries, withCard, type DialogEntry } from "@/hooks/useSpecDialog";
import type { SpecDialogProposalPush, SpecDialogView } from "@/types/spec-dialog";

// 2026-11-10-7da6: the conversation shows one card for the current proposal — live while a turn re-drafts it,
// and after a reload in the middle of a turn.
const draft = (id: string): SpecDialogProposalPush =>
  ({ dialogId: "d-1", kind: "spec", at: `2026-11-10T10:0${id.length}:00Z`, spec: { specId: id } }) as unknown as SpecDialogProposalPush;
const user = (text: string): DialogEntry => ({ key: `u-${text}`, kind: "user", text, at: "2026-11-10T10:00:00Z" });
const agent = (text: string, proposal?: SpecDialogProposalPush): DialogEntry =>
  ({ key: `a-${text}`, kind: "agent", text, at: "2026-11-10T10:00:30Z", ...(proposal ? { proposal } : {}) });
const cards = (held: DialogEntry[]) => held.filter((entry) => entry.proposal);

function view(over: { computing: boolean; proposal?: SpecDialogProposalPush; reviewing?: SpecDialogProposalPush; proposalTurn: number | null; turns: number }): SpecDialogView {
  return {
    dialogId: "d-1", projects: [], question: null,
    turn: { computing: over.computing } as SpecDialogView["turn"],
    session: {
      proposal: over.proposal ?? null, reviewing: over.reviewing ?? null, proposalTurn: over.proposalTurn,
      transcript: Array.from({ length: over.turns }, (_, i) => ({ role: i % 2 ? "assistant" : "user", text: `t${i}`, at: "x" })),
    } as unknown as SpecDialogView["session"],
  };
}

describe("one card per proposal", () => {
  it("useSpecDialog_SeveralDraftsInOneLiveTurn_ShowOneCard", () => {
    let held = [user("draft it"), agent("Here is the cut.")];
    held = withCard(held, draft("p1"), false);
    held = withCard(held, draft("p12"), false);
    held = withCard(held, draft("p123"), false);
    expect(cards(held)).toHaveLength(1);
    expect(cards(held)[0].proposal).toEqual(draft("p123"));
  });

  it("useSpecDialog_ReloadMidTurnFreshConversation_ShowsOneCardAtTheEnd", () => {
    const reviewing = draft("p1");
    expect(trailingCard(view({ computing: true, reviewing, proposalTurn: null, turns: 1 }))).toBe(reviewing);
  });

  it("useSpecDialog_ReloadMidTurnWithEarlierDraftTurn_CardAtTheEndOnly", () => {
    const awaiting = draft("p1");
    const reviewing = draft("p12");
    const read = view({ computing: true, proposal: awaiting, reviewing, proposalTurn: 1, turns: 3 });
    expect(trailingCard(read)).toBe(reviewing);
  });

  it("useSpecDialog_ReloadMidTurnThenLivePush_StillOneCard", () => {
    const held = [user("go"), agent("", draft("p1"))];
    const after = withCard(held, draft("p12"), true);
    expect(cards(after)).toHaveLength(1);
    expect(cards(after)[0].proposal).toEqual(draft("p12"));
  });

  it("useSpecDialog_CardAnsweredByDecision_StaysAndNewTurnAddsItsOwn", () => {
    const answered: DialogEntry = { key: "d", kind: "decision", text: "reject", at: "x", decision: "rejected" };
    const held = [agent("cut", draft("p1")), answered];
    const after = withCard(held, draft("p12"), true);
    expect(cards(after).map((entry) => entry.proposal)).toEqual([draft("p1"), draft("p12")]);
  });

  it("useSpecDialog_StoredProposalWithItsTurn_NoTrailingCard", () => {
    expect(trailingCard(view({ computing: false, proposal: draft("p1"), proposalTurn: 1, turns: 2 }))).toBeNull();
  });

  // 2026-11-10-d854b: a turn that died after a draft has no kept turn; its last draft is still its result.
  it("useSpecDialog_CrashedTurnsDraft_IsTheTrailingResult", () => {
    const read = view({ computing: false, proposalTurn: null, turns: 2 });
    (read.session as unknown as { runningProposal: SpecDialogProposalPush }).runningProposal = draft("p9");
    expect(trailingCard(read)).toEqual(draft("p9"));
    expect(trailingCard({ ...read, turn: { computing: true } as SpecDialogView["turn"] })).toBeNull();
  });

  // 2026-11-12-01b4: a tool-written proposal is carried by its reply's run (d854c). Unanchored, the awaiting proposal
  // trailed as a second, text-less copy that took the approval; anchored on its reply, it is shown once, with its id.
  it("useSpecDialog_AwaitingProposalOnItsKeptReply_RendersOnceWithItsId", () => {
    const kept = draft("p1");
    const awaiting = { ...kept, proposalId: "p-1" };
    const read = (proposalTurn: number | null) => {
      const shaped = view({ computing: false, proposal: awaiting, proposalTurn, turns: 2 });
      const session = shaped.session as unknown as { transcript: Array<Record<string, unknown>> };
      session.transcript[1] = { ...session.transcript[1], text: "Worked it out.", proposal: kept };
      return shaped;
    };

    expect(cards(transcriptEntries(read(null)))).toHaveLength(2);
    const anchored = cards(transcriptEntries(read(1)));
    expect(anchored).toHaveLength(1);
    expect(anchored[0].text).toBe("Worked it out.");
    expect(anchored[0].proposal?.proposalId).toBe("p-1");
  });
});
