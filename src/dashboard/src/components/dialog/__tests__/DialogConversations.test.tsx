import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";

import type { SpecDialogSessionSummary } from "@/types/spec-dialog";
import { DialogConversations } from "../DialogConversations";
import { outcomeTone } from "../conversationRows";

// 2026-11-17-d762a: the conversations are the page's own section bar — New conversation and All conversations as
// rows, one row per conversation, the open one marked, every fact as text.

function conversation(overrides: Partial<SpecDialogSessionSummary> = {}): SpecDialogSessionSummary {
  return {
    sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: new Date().toISOString(),
    title: "a widget that reads the ledger", subject: null, outcome: null, openDialogId: null, ticket: null,
    ...overrides,
  };
}

function renderBar(conversations: SpecDialogSessionSummary[], sessionHere: string | null = null) {
  const handlers = { onStartNew: vi.fn(), onOpen: vi.fn(), onDelete: vi.fn() };
  render(<DialogConversations dialogId="d-1" sessionHere={sessionHere} conversations={conversations} {...handlers} />);
  return handlers;
}

describe("DialogConversations", () => {
  it("DialogConversations_NewConversation_IsAPlainRowThatStartsOne", () => {
    const { onStartNew } = renderBar([]);
    const row = screen.getByTestId("dialog-new");

    expect(row).toHaveTextContent("New conversation");
    expect(row.className).toBe("dl-bar-new");
    fireEvent.click(row);
    expect(onStartNew).toHaveBeenCalledTimes(1);
    expect(row).toBeDisabled();
  });

  it("DialogConversations_ARow_OpensItsConversationAndMarksTheOpenOne", () => {
    const { onOpen } = renderBar(
      [conversation({ sessionId: "s-1", openDialogId: "d-7" }), conversation({ sessionId: "s-2" })], "s-1");

    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("dialog-conversation-s-2")).not.toHaveAttribute("aria-current");
    fireEvent.click(screen.getByTestId("dialog-conversation-s-2"));
    expect(onOpen).toHaveBeenCalledWith("s-2", null);
    fireEvent.click(screen.getByTestId("dialog-conversation-s-1"));
    expect(onOpen).toHaveBeenCalledWith("s-1", "d-7");
  });

  it("DialogConversations_AllConversations_LinksToTheListAtTheFoot", () => {
    renderBar([conversation()]);
    const all = screen.getByTestId("dialog-see-all");

    expect(all).toHaveAttribute("href", "/spec-dialog/conversations");
    expect(screen.getByTestId("dialog-controls").lastElementChild).toBe(all);
  });

  it("DialogConversations_ARowsFacts_AreTextWithTheOutcomeInItsColour", () => {
    renderBar([conversation({ ticket: "GH-12", outcome: { kind: "epic", tickets: 2, partial: true } })]);
    const row = screen.getByTestId("dialog-conversation-s-9");

    expect(within(row).getByTestId("dialog-conversation-ticket-s-9")).toHaveTextContent("GH-12");
    expect(within(row).getByTestId("dialog-conversation-outcome")).toHaveClass("dl-tone", "run");
    expect(screen.getByTestId("dialog-controls").querySelector(".ecard, .ec-mark, .btn")).toBeNull();
  });

  it("DialogConversations_TheDelete_StaysBesideTheRow", () => {
    const { onDelete } = renderBar([conversation()]);
    fireEvent.click(screen.getByTestId("dialog-delete-s-9"));
    expect(onDelete).toHaveBeenCalledWith("s-9");
  });
});

describe("outcomeTone", () => {
  it("outcomeTone_PartialIsAmber_WholeIsGreen_NoneIsPlain", () => {
    expect(outcomeTone(conversation({ outcome: { kind: "bug", tickets: 1, partial: true } }))).toBe("run");
    expect(outcomeTone(conversation({ outcome: { kind: "bug", tickets: 1, partial: false } }))).toBe("ok");
    expect(outcomeTone(conversation())).toBe("");
  });
});
