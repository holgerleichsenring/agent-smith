import { describe, expect, it } from "vitest";
import type { SpecDialogTurn, SpecDialogView } from "@/types/spec-dialog";
import { replyRecorded } from "../recordedReply";

const turn = (role: string): SpecDialogTurn => ({ role, text: role, at: "2016-01-01T00:00:00Z" });

function view(roles: string[], computing = false): SpecDialogView {
  return {
    dialogId: "d-1", projects: [], question: null,
    session: { transcript: roles.map(turn) } as unknown as SpecDialogView["session"],
    turn: { computing, elapsedSeconds: 0, steps: [], turnStartedAt: null },
  };
}

describe("replyRecorded", () => {
  const awaited = { read: 3, turns: 2 };

  it("AReadAfterThePost_WithALongerTranscriptEndingInTheAgent_IsTheReply", () => {
    expect(replyRecorded(view(["user", "assistant", "user", "assistant"]), awaited, 4)).toBe(true);
  });

  it("TheReplyBeforeThePostStillLast_TheMessageAloneStored_OrTheTurnStillComputing_IsNot", () => {
    expect(replyRecorded(view(["user", "assistant"]), awaited, 4)).toBe(false);
    expect(replyRecorded(view(["user", "assistant", "user"]), awaited, 4)).toBe(false);
    expect(replyRecorded(view(["user", "assistant", "user", "assistant"], true), awaited, 4)).toBe(false);
  });

  it("AReadIssuedBeforeThePost_OrNoPostAwaited_IsNot", () => {
    expect(replyRecorded(view(["user", "assistant", "user", "assistant"]), awaited, 3)).toBe(false);
    expect(replyRecorded(view(["user", "assistant", "user", "assistant"]), null, 4)).toBe(false);
  });
});
