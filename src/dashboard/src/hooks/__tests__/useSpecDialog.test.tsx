import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { INSTANCE_DIALOG_ID } from "@/lib/specDialogSession";
import type { SpecDialogActivityPush, SpecDialogView } from "@/types/spec-dialog";

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
