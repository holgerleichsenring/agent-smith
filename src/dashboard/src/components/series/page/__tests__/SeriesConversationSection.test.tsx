import { StrictMode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-11-23-31dcd: with Work it out's real hook, the section lands on the conversation's own thread — also under
// StrictMode, whose second effect pass once moved it onto a fresh dialog id — and reads no other dialog.

const hub = vi.hoisted(() => {
  const subject = () => ({ add: () => () => {}, emit: () => {} });
  return {
    specDialogMessages: subject(), specDialogQuestions: subject(), specDialogProposals: subject(),
    specDialogFilings: subject(), specDialogReadings: subject(), specDialogActivity: subject(), connectionState: subject(),
    joins: [] as string[],
    async subscribeSpecDialog(id: string) { this.joins.push(id); return async () => {}; },
  };
});
const api = vi.hoisted(() => ({ fetched: [] as string[], question: null as SpecDialogQuestionPush | null }));
vi.mock("@/lib/JobsHubClient", () => ({ getJobsHubClient: () => hub }));
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: async (id: string) => {
    api.fetched.push(id);
    return {
      dialogId: id, projects: [], question: api.question, turn: null,
      session: id === "d-thread1" ? { sessionId: "conv-1", transcript: [], scope: { name: "shop", repos: [], templates: [] }, images: [] } : null,
    };
  },
  fetchSpecDialogConversations: async () => ({ conversations: [], total: 0 }),
  postSpecDialogMessage: async () => {}, resumeSpecDialogConversation: async () => {},
  deleteSpecDialogConversation: async () => {}, uploadSpecDialogImage: async () => ({}),
  uploadSpecDialogReferences: async () => ({}), fetchDialogActivity: async () => ({ steps: [] }),
  specDialogImageUrl: () => "",
}));

import { SeriesConversationSection, threadSource } from "../SeriesConversationSection";
import { seriesDetail } from "./seriesFixtures";

describe("SeriesConversationSection", () => {
  it.each([false, true])("SeriesConversation_StrictMode%s_LandsOnTheConversationsThread", async (strict) => {
    api.fetched.length = 0;
    const ui = <SeriesConversationSection detail={seriesDetail()} />;
    render(strict ? <StrictMode>{ui}</StrictMode> : ui);

    await waitFor(() => expect(screen.getByTestId("dialog-composer-text")).not.toBeDisabled());
    expect(new Set(api.fetched)).toEqual(new Set(["d-thread1"]));
  });

  it("SeriesConversation_AProposalWaitingForADecision_SendsTheReaderToWorkItOut", async () => {
    api.question = { kind: "approval", expiresAt: null } as unknown as SpecDialogQuestionPush;
    render(<SeriesConversationSection detail={seriesDetail()} />);

    expect(await screen.findByTestId("series-talk-decide")).toHaveTextContent("waits for a decision");
    expect(screen.queryByTestId("dialog-composer-text")).toBeNull();
    api.question = null;
  });

  it("SeriesConversation_NoThread_SaysTheConversationIsGone", () => {
    render(<SeriesConversationSection detail={seriesDetail({ threadId: null })} />);

    expect(screen.getByTestId("series-talk-gone")).toBeInTheDocument();
  });

  it("threadSource_StartsAndStaysOnTheThread", () => {
    const source = threadSource("d-1");
    expect(source.seed()).toBe("d-1");
    expect(source.switchTo()).toBe("d-1");
    expect(source.switchTo("d-2")).toBe("d-2");
  });
});
