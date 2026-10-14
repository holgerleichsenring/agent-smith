import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { INSTANCE_DIALOG_ID } from "@/lib/specDialogSession";
import type { SpecDialogMock, SpecDialogView } from "@/types/spec-dialog";

// 2026-10-13-8762r: a mock the agent wrote arms no reseed, so the read after its reply has to merge it —
// otherwise the operator could not answer the version the turn just showed without reloading.

const hub = vi.hoisted(() => ({ onMessage: null as null | ((m: { dialogId: string; text: string; at: string }) => void) }));
const subject = () => ({ add: () => () => {} });
vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: { add: (handler: typeof hub.onMessage) => ((hub.onMessage = handler), () => {}) },
    specDialogQuestions: subject(), specDialogProposals: subject(),
    specDialogFilings: subject(), specDialogReadings: subject(), specDialogActivity: subject(),
    connectionState: subject(),
    subscribeSpecDialog: async () => async () => {},
  }),
}));

const api = vi.hoisted(() => ({ fetch: vi.fn() }));
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (id: string) => api.fetch(id),
  fetchSpecDialogConversations: async () => ({ conversations: [], total: 0 }),
  postSpecDialogMessage: async () => {},
  resumeSpecDialogConversation: async () => {},
  deleteSpecDialogConversation: async () => {},
  uploadSpecDialogImage: async () => ({}),
  uploadSpecDialogReferences: async () => ({}),
}));

const { useSpecDialog } = await import("../useSpecDialog");

function view(dialogId: string, mocks: SpecDialogMock[]): SpecDialogView {
  return {
    dialogId, projects: [], question: null,
    turn: { computing: false, elapsedSeconds: 0, steps: [], turnStartedAt: null },
    session: {
      sessionId: "s-1", ticket: null, scope: { name: "sample", repos: [], templates: [] },
      transcript: [{ role: "user", text: "show me the page", at: "2026-10-13T10:00:00Z" }],
      lastActivityAt: "2026-10-13T10:00:00Z", subject: null, proposal: null, filing: null, proposalTurn: null,
      images: [], mocks,
    },
  } as SpecDialogView;
}

const v1: SpecDialogMock = { specId: "spec-a", version: 1, name: "spec-a-mock-v1", at: "2026-10-13T10:01:00Z", hasShot: true };

beforeEach(() => {
  hub.onMessage = null;
  api.fetch.mockReset();
});

describe("useSpecDialog with mocks", () => {
  it("useSpecDialog_ReplyWithNewMock_ShowsItWithoutReload", async () => {
    let mocks: SpecDialogMock[] = [];
    api.fetch.mockImplementation(async (id: string) => view(id, mocks));
    const { result } = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(result.current.entries.map((e) => e.kind)).toEqual(["user"]));
    await waitFor(() => expect(hub.onMessage).not.toBeNull());

    mocks = [v1];
    await act(async () => hub.onMessage!({ dialogId: result.current.dialogId!, text: "Mock v1 is above.", at: "2026-10-13T10:02:00Z" }));

    await waitFor(() => expect(result.current.entries.map((e) => e.key)).toContain("mock-spec-a-v1"));
    const entry = result.current.entries.find((e) => e.kind === "mock");
    expect(entry?.mock).toMatchObject({ sessionId: "s-1", specId: "spec-a", version: 1 });
    expect(result.current.entries.filter((e) => e.kind === "agent").map((e) => e.text)).toEqual(["Mock v1 is above."]);
  });
});
