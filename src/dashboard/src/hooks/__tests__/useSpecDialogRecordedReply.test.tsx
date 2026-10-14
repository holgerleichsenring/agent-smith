import { act, renderHook, waitFor } from "@testing-library/react";
import { HubConnectionState } from "@microsoft/signalr";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { INSTANCE_DIALOG_ID } from "@/lib/specDialogSession";
import type { SpecDialogTurn, SpecDialogView } from "@/types/spec-dialog";

// The stored reply ends the wait: a push is a hint, and a read issued after the post that shows the
// reply recorded is the fact. And a join the hub refused while it reconnected is issued again once
// it is back.

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

const server = { transcript: [] as SpecDialogTurn[], computing: false };

// A server clock a decade away from this browser's: nothing here may compare the two.
const turn = (role: string, text: string): SpecDialogTurn => ({ role, text, at: "2016-01-01T00:00:00Z" });

function view(dialogId: string): SpecDialogView {
  return {
    dialogId, projects: [], question: null,
    session: {
      sessionId: "s-1", ticket: null, scope: { name: "shop", repos: [], templates: [] },
      transcript: [...server.transcript], lastActivityAt: "2016-01-01T00:00:00Z", subject: null,
      proposal: null, filing: null, proposalTurn: null, images: [],
    } as unknown as SpecDialogView["session"],
    turn: {
      computing: server.computing, elapsedSeconds: server.computing ? 4 : 0, steps: [],
      turnStartedAt: server.computing ? "2016-01-01T00:00:00Z" : null,
    },
  };
}

async function posted() {
  const rendered = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
  await waitFor(() => expect(rendered.result.current.view).not.toBeNull());
  await act(() => rendered.result.current.send("Why did it fail?", "shop"));
  expect(rendered.result.current.working).toBe(true);
  return rendered;
}

const reconnect = () => act(() => hub.connectionState.emit(HubConnectionState.Connected));

beforeEach(() => {
  hub.failJoins = 0;
  hub.joins = [];
  server.transcript = [turn("user", "Hello"), turn("assistant", "Hi, what is it about?")];
  server.computing = false;
  api.fetch.mockReset();
  api.fetch.mockImplementation(async (id: string) => view(id));
  api.post.mockClear();
  api.resume.mockClear();
});

describe("useSpecDialog, the recorded reply", () => {
  it("AReadAfterThePost_ShowingTheReplyStored_EndsTheWaitWithoutAPush", async () => {
    const { result } = await posted();
    server.transcript = [...server.transcript, turn("user", "Why did it fail?"), turn("assistant", "The build timed out.")];

    await reconnect();

    await waitFor(() => expect(result.current.working).toBe(false));
    expect(result.current.awaiting).toBe(false);
    expect(result.current.workingSince).toBeNull();
  });

  it("AReadAfterThePost_WithThePreviousReplyStillLast_KeepsWaiting", async () => {
    const { result } = await posted();

    await reconnect();
    await waitFor(() => expect(api.fetch).toHaveBeenCalledTimes(2));

    expect(result.current.awaiting).toBe(true);
  });

  it("AReadAfterThePost_WithTheMessageStoredButNoReplyYet_KeepsWaiting", async () => {
    const { result } = await posted();
    server.transcript = [...server.transcript, turn("user", "Why did it fail?")];

    await reconnect();
    await waitFor(() => expect(api.fetch).toHaveBeenCalledTimes(2));

    expect(result.current.awaiting).toBe(true);
  });

  it("AReadShowingTheReplyWhileTheServerStillComputes_KeepsWaiting", async () => {
    const { result } = await posted();
    server.transcript = [...server.transcript, turn("user", "Why did it fail?"), turn("assistant", "Looking…")];
    server.computing = true;

    await reconnect();
    await waitFor(() => expect(api.fetch).toHaveBeenCalledTimes(2));

    expect(result.current.awaiting).toBe(true);
  });

  it("AJoinRefusedWhileTheHubReconnected_IsReissuedOnConnected_AndClearsTheFailure", async () => {
    hub.failJoins = 1;
    const { result } = renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(result.current.failure).not.toBeNull());

    await reconnect();

    await waitFor(() => expect(hub.joins).toHaveLength(2));
    expect(hub.joins[1]).toBe(hub.joins[0]);
    await waitFor(() => expect(result.current.failure).toBeNull());
  });

  it("AJoinThatSucceeded_IsNotReissuedOnConnected", async () => {
    renderHook(() => useSpecDialog(INSTANCE_DIALOG_ID));
    await waitFor(() => expect(hub.joins).toHaveLength(1));

    await reconnect();
    await waitFor(() => expect(api.fetch).toHaveBeenCalledTimes(2));

    expect(hub.joins).toHaveLength(1);
  });
});
