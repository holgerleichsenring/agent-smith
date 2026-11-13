import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { INSTANCE_DIALOG_ID } from "@/lib/specDialogSession";
import type { SpecDialogActivityPush, SpecDialogProposalPush, SpecDialogView } from "@/types/spec-dialog";

// 2026-11-12-a1fa: a message sent while the turn computes reaches that turn with its next tool result,
// and the turn carries on — so the page keeps the turn's steps and its clock, and the turn's own reply
// ends the wait.

const hub = vi.hoisted(() => {
  const subject = <T,>() => {
    const listeners = new Set<(value: T) => void>();
    return {
      add: (listener: (value: T) => void) => { listeners.add(listener); return () => listeners.delete(listener); },
      emit: (value: T) => { for (const listener of [...listeners]) listener(value); },
    };
  };
  return {
    specDialogMessages: subject<unknown>(), specDialogQuestions: subject<unknown>(),
    specDialogProposals: subject<unknown>(), specDialogFilings: subject<unknown>(),
    specDialogReadings: subject<unknown>(), specDialogActivity: subject<unknown>(),
    connectionState: subject<string>(),
    failJoins: 0,
    joins: [] as string[],
    async subscribeSpecDialog(dialogId: string) {
      this.joins.push(dialogId);
      if (this.failJoins > 0) {
        this.failJoins -= 1;
        throw new Error("Invocation canceled due to the underlying connection being closed.");
      }
      return async () => {};
    },
  };
});
vi.mock("@/lib/JobsHubClient", () => ({ getJobsHubClient: () => hub }));

const api = vi.hoisted(() => ({
  fetch: vi.fn(),
  post: vi.fn(async () => {}),
  resume: vi.fn(async () => {}),
}));
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (id: string) => api.fetch(id),
  fetchSpecDialogConversations: async () => ({ conversations: [], total: 0 }),
  postSpecDialogMessage: (...args: unknown[]) => api.post(...(args as [])),
  resumeSpecDialogConversation: (...args: unknown[]) => api.resume(...(args as [])),
  deleteSpecDialogConversation: async () => {},
  uploadSpecDialogImage: async () => ({}),
  uploadSpecDialogReferences: async () => ({}),
}));

const { useSpecDialog } = await import("../useSpecDialog");

const started = "2016-01-01T00:00:00Z";
const step: SpecDialogActivityPush = {
  dialogId: "d-1", kind: "tool", name: "read_file", detail: "src/import.ts", at: started, seq: 1,
  turnStartedAt: started,
};

function view(dialogId: string): SpecDialogView {
  return {
    dialogId, projects: [], question: null,
    session: {
      sessionId: "s-1", ticket: null, scope: { name: "shop", repos: [], templates: [] },
      transcript: [{ role: "user", text: "Why did it fail?", at: started }], lastActivityAt: started, subject: null,
      proposal: null, filing: null, proposalTurn: null, images: [],
    } as unknown as SpecDialogView["session"],
    turn: { computing: true, elapsedSeconds: 30, steps: [step], turnStartedAt: started },
  };
}

beforeEach(() => {
  api.fetch.mockReset();
  api.fetch.mockImplementation(async (id: string) => view(id));
  api.post.mockClear();
});

describe("useSpecDialog, a send while the turn computes", () => {
  it("SendWhileComputing_KeepsActivity", async () => {
    const { result } = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(result.current.activity).toHaveLength(1));
    const since = result.current.workingSince;
    expect(since).not.toBeNull();

    await act(() => result.current.send("Also check the retry path", "shop"));

    expect(api.post).toHaveBeenCalledTimes(1);
    expect(result.current.activity).toEqual([step]);
    expect(result.current.workingSince).toBe(since);
    expect(result.current.working).toBe(true);
  });
});

// 2026-11-12-92e9c: a written turn never changes. A draft under review stays out of the conversation; the final proposal
// joins its turn's reply with the steps the page watched, and filing it later takes nothing from that turn.
const draft = (dialogId: string, reviewing: boolean): SpecDialogProposalPush => ({
  dialogId, kind: "spec", bug: null, parent: null, children: [], at: started, reviewing,
  spec: { specId: "s-1", goal: "Retry the import", steps: [], tests: [], done: [], requires: [] },
} as unknown as SpecDialogProposalPush);

describe("useSpecDialog, a turn once shown", () => {
  async function replied() {
    const hook = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(hook.result.current.activity).toHaveLength(1));
    const dialogId = hook.result.current.dialogId!;
    act(() => hub.specDialogProposals.emit(draft(dialogId, true)));
    act(() => hub.specDialogProposals.emit(draft(dialogId, true)));
    expect(hook.result.current.entries.filter((entry) => entry.proposal)).toHaveLength(0);
    act(() => hub.specDialogMessages.emit({ dialogId, title: "t", text: "Here is the cut.", at: started }));
    act(() => hub.specDialogProposals.emit(draft(dialogId, false)));
    return { ...hook, dialogId };
  }

  it("ReviewingDraft_NotInTranscript_FinalAttachesToReply", async () => {
    const { result } = await replied();

    const shown = result.current.entries.filter((entry) => entry.proposal);
    expect(shown).toHaveLength(1);
    expect(shown[0].text).toBe("Here is the cut.");
    expect(shown[0].proposal?.reviewing).toBe(false);
    expect(shown[0].steps).toEqual([step]);
  });

  it("ShownTurn_KeepsItsProposal_AfterFiling", async () => {
    const { result, dialogId } = await replied();
    await waitFor(() => expect(api.fetch).toHaveBeenCalledTimes(2));
    api.fetch.mockImplementation(async (id: string) => {
      const read = view(id);
      return { ...read, turn: null, session: { ...read.session!, filing: { dialogId: id, at: started, created: [] } } } as SpecDialogView;
    });

    act(() => hub.specDialogMessages.emit({ dialogId, title: "t", text: "Filed.", at: started }));

    await waitFor(() => expect(result.current.proposal).toBeNull());
    const turn = result.current.entries.find((entry) => entry.text === "Here is the cut.");
    expect(turn?.proposal?.spec?.specId).toBe("s-1");
  });
});
