import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

import type { SpecDialogSessionSummary } from "@/types/spec-dialog";
import { ApiRefusal } from "@/lib/apiResponse";
import { ConversationsView, PAGE_LIMIT } from "../ConversationsView";
import { conversationHref } from "../conversationHref";

// 2026-09-21-f237b: the panel beside the exchange was the only place a past conversation could be
// reached. This page is the second, and the first one with an address.

const fetchSpecDialogConversations = vi.fn();
const deleteSpecDialogConversation = vi.fn(async () => {});
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialogConversations: (limit?: number, signal?: AbortSignal) =>
    fetchSpecDialogConversations(limit, signal),
  deleteSpecDialogConversation: (sessionId: string) => deleteSpecDialogConversation(sessionId),
}));

function conversation(overrides: Partial<SpecDialogSessionSummary> = {}): SpecDialogSessionSummary {
  return {
    sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: new Date().toISOString(),
    title: "a widget that reads the ledger", subject: null, outcome: null, openDialogId: null,
    ...overrides,
  };
}

const listing = (conversations: SpecDialogSessionSummary[], total = conversations.length) =>
  ({ conversations, total });

beforeEach(() => {
  fetchSpecDialogConversations.mockReset();
  fetchSpecDialogConversations.mockResolvedValue(listing([conversation()]));
  deleteSpecDialogConversation.mockClear();
  window.confirm = () => true;
});

describe("The conversations page", () => {
  it("ConversationsPage_TheCallersConversations_AreListedWithSubjectProjectTurnsAndOutcome", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: "Ich brauche alle libraries aktualisiert",
        subject: "Aktualisierung aller Projektbibliotheken", turns: 4,
        outcome: { kind: "phase", tickets: 2, partial: false },
      }),
    ]));

    render(<ConversationsView />);

    const row = await screen.findByTestId("conversations-open-s-1");
    expect(row).toHaveTextContent("Aktualisierung aller Projektbibliotheken");
    expect(row).toHaveTextContent("sample");
    expect(row).toHaveTextContent("4 turns");
    expect(row).toHaveTextContent("phase filed · 2 tickets");
  });

  // The point of the page is the rows the panel does not show, so it asks for the ceiling rather
  // than the server's own default.
  it("ConversationsPage_ItsRead_AsksForTheCeiling", async () => {
    render(<ConversationsView />);

    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    expect(fetchSpecDialogConversations.mock.calls[0][0]).toBe(PAGE_LIMIT);
  });

  // A row hands the surface a session id — and, where the row says the conversation is OPEN, the
  // dialog id it is living on, which is the only thing that tells `open` to go there rather than
  // resume it. A resume is refused while a turn runs.
  it("ConversationsPage_ARow_LinksToTheSurfaceCarryingItsSessionId", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", openDialogId: null }),
      conversation({ sessionId: "s-2", openDialogId: "d-7" }),
    ]));

    render(<ConversationsView />);

    expect(await screen.findByTestId("conversations-open-s-1"))
      .toHaveAttribute("href", "/spec-dialog?open=s-1");
    expect(screen.getByTestId("conversations-open-s-2"))
      .toHaveAttribute("href", "/spec-dialog?open=s-2&on=d-7");
  });

  it("ConversationHref_AnOpenConversation_CarriesTheDialogItLivesOn", () => {
    expect(conversationHref(conversation({ sessionId: "s-3", openDialogId: "d-1" })))
      .toBe("/spec-dialog?open=s-3&on=d-1");
    expect(conversationHref(conversation({ sessionId: "s-3", openDialogId: null })))
      .toBe("/spec-dialog?open=s-3");
  });

  // The delete lived on the panel row and nowhere else, so a conversation the panel does not draw
  // could not be deleted at all — which is what lets f237c bound the panel.
  it("ConversationsPage_ARow_DeletesAfterTheSameConfirmation", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-1" })]));

    render(<ConversationsView />);
    fireEvent.click(await screen.findByTestId("conversations-delete-s-1"));

    const confirmation = await screen.findByTestId("confirm-dialog");
    expect(confirmation).toHaveTextContent("a widget that reads the ledger");
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));

    await waitFor(() => expect(deleteSpecDialogConversation).toHaveBeenCalledWith("s-1"));
    await waitFor(() =>
      expect(screen.queryByTestId("conversations-open-s-1")).not.toBeInTheDocument());
  });

  // "I received exactly what I asked for" cannot tell a full page from a caller who holds exactly
  // that many, so the count is served rather than inferred.
  it("ConversationsPage_MoreThanShown_SaysHowManyOfHowMany", async () => {
    fetchSpecDialogConversations.mockResolvedValue(
      listing([conversation({ sessionId: "s-1" })], 63));

    render(<ConversationsView />);

    expect(await screen.findByTestId("conversations-count"))
      .toHaveTextContent("the 1 most recent of 63");
  });

  it("ConversationsPage_AllOfThem_SaysSoWithoutClaimingToBeAPage", async () => {
    fetchSpecDialogConversations.mockResolvedValue(
      listing([conversation({ sessionId: "s-1" })], 1));

    render(<ConversationsView />);

    expect(await screen.findByTestId("conversations-count")).toHaveTextContent("1 in all");
  });

  it("ConversationsPage_NoConversations_SaysSo", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([]));

    render(<ConversationsView />);

    expect(await screen.findByTestId("conversations-none")).toBeInTheDocument();
  });

  // The route needs the dialog permission and the rail offers Work it out to everyone, so a
  // caller without it must be told which permission is missing — not shown what looks like an
  // empty history.
  it("ConversationsPage_ARefusedRead_SaysWhichPermission", async () => {
    fetchSpecDialogConversations.mockRejectedValue(
      new ApiRefusal("/api/spec-dialog/conversations", 403, "permission", ["dialog.write"]));

    render(<ConversationsView />);

    expect(await screen.findByTestId("refusal-surface")).toHaveTextContent("dialog.write");
  });
});
