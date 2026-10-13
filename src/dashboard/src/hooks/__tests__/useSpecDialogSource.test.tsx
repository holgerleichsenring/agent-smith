import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HELD_DIALOG_ID, INSTANCE_DIALOG_ID, __forgetDialogIdForTests } from "@/lib/specDialogSession";
import type { SpecDialogView } from "@/types/spec-dialog";

// 2026-10-13-9a59r: where useSpecDialog takes its dialog id from. The default holds the browser's one id
// (Work it out); the instance source mints per mount and holds nothing, so a ticket's conversation never
// reads or rewrites what Work it out holds.

const subject = () => ({ add: () => () => {} });
vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: subject(), specDialogQuestions: subject(), specDialogProposals: subject(),
    specDialogFilings: subject(), specDialogReadings: subject(), specDialogActivity: subject(),
    connectionState: subject(),
    subscribeSpecDialog: async () => async () => {},
  }),
}));

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

const STORAGE_KEY = "agentsmith.spec-dialog.dialog-id";

function emptyView(dialogId: string): SpecDialogView {
  return {
    dialogId, session: null, projects: [], question: null,
    turn: { computing: false, elapsedSeconds: 0, steps: [], turnStartedAt: null },
  };
}

// This jsdom build ships no localStorage; the held id is the subject here, so a Map backs it.
function stubStorage(): void {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (key: string) => backing.get(key) ?? null,
      setItem: (key: string, value: string) => void backing.set(key, value),
      removeItem: (key: string) => void backing.delete(key),
      clear: () => backing.clear(),
    },
  });
}

beforeEach(() => {
  __forgetDialogIdForTests();
  stubStorage();
  api.fetch.mockReset();
  api.fetch.mockImplementation(async (id: string) => emptyView(id));
  api.post.mockClear();
  api.resume.mockClear();
});

describe("useSpecDialog's dialog-id source", () => {
  it("TheInstanceSource_LeavesTheHeldWorkItOutDialogIdUnchanged_OnMountOpenAndStartNew", async () => {
    window.localStorage.setItem(STORAGE_KEY, "d-held");
    const { result } = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(result.current.dialogId).not.toBeNull());
    expect(result.current.dialogId).not.toBe("d-held");

    await act(() => result.current.open("s-ticket", "d-open"));
    await waitFor(() => expect(result.current.dialogId).toBe("d-open"));
    await act(() => result.current.startNew());
    await waitFor(() => expect(result.current.dialogId).not.toBe("d-open"));

    expect(window.localStorage.getItem(STORAGE_KEY)).toBe("d-held");
  });

  it("TheInstanceSource_FirstMessage_GoesToAFreshlyMintedIdWithProjectAndTicket_EvenWhenTheHeldIdHasAConversation", async () => {
    window.localStorage.setItem(STORAGE_KEY, "d-held");
    api.fetch.mockImplementation(async (id: string) => id === "d-held"
      ? { ...emptyView(id), session: { sessionId: "s-workitout" } as SpecDialogView["session"] }
      : emptyView(id));
    const { result } = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(result.current.view).not.toBeNull());

    await act(() => result.current.send("Add the import consumer", "shop", undefined, "101"));

    expect(api.post).toHaveBeenCalledTimes(1);
    const [dialogId, text, project, ticketId] = api.post.mock.calls[0] as unknown as [string, string, string, string];
    expect(dialogId).not.toBe("d-held");
    expect(dialogId).toMatch(/^d-/);
    expect([text, project, ticketId]).toEqual(["Add the import consumer", "shop", "101"]);
    expect(api.fetch).not.toHaveBeenCalledWith("d-held");
  });

  it("TheDefaultSource_StillHoldsAndRestoresTheBrowsersDialogId", async () => {
    window.localStorage.setItem(STORAGE_KEY, "d-held");
    const first = renderHook(() => useSpecDialog());
    await waitFor(() => expect(first.result.current.dialogId).toBe("d-held"));
    await act(() => first.result.current.startNew());
    await waitFor(() => expect(first.result.current.dialogId).not.toBe("d-held"));
    const minted = first.result.current.dialogId;
    expect(window.localStorage.getItem(STORAGE_KEY)).toBe(minted);
    first.unmount();

    __forgetDialogIdForTests();
    const again = renderHook(() => useSpecDialog(HELD_DIALOG_ID));
    await waitFor(() => expect(again.result.current.dialogId).toBe(minted));
  });
});
