import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "@/lib/apiResponse";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcd: the series page; its chat posts to the series' own conversation. 2026-11-25-9992j: ONE pane — the graph
// and the chat under it, no sections; an older ?section=talk link scrolls to the chat; the series' waiting question shows
// its choices as buttons, the recommended one primary, and a click sends the choice as the answer.

const hoisted = vi.hoisted(() => ({
  search: "", fetch: vi.fn(), lacks: false, scrolled: vi.fn(),
  dialog: { open: vi.fn(), send: vi.fn(), attach: vi.fn(), entries: [], working: false, readings: [], activity: [],
    workingSince: null, uploadNote: null, failure: null, awaitingProposal: null, question: null,
    view: { session: { sessionId: "conv-1" } } as unknown },
}));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a) }));
vi.mock("@/hooks/useSpecDialog", () => ({ useSpecDialog: () => hoisted.dialog }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));

import { SeriesRunPage } from "../SeriesRunPage";

const asked = () => seriesDetail({ question: "#50 is ready to merge. Shall I merge, or do you want to look at the PR first?",
  questionChoices: ["Merge it", "I'll look at the PR first"], questionRecommended: 0 });

beforeEach(() => {
  hoisted.search = "";
  hoisted.lacks = false;
  hoisted.dialog.open.mockReset();
  hoisted.dialog.send.mockReset();
  hoisted.scrolled.mockReset();
  hoisted.fetch.mockReset().mockResolvedValue(seriesDetail());
  Element.prototype.scrollIntoView = hoisted.scrolled;
});

describe("SeriesRunPage", () => {
  it("SeriesPage_IsTheGraphAndTheChat_OnOnePane_WithoutSections", async () => {
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-graph")).toBeInTheDocument();
    expect(screen.getByTestId("series-chat")).toContainElement(screen.getByTestId("series-talk"));
    expect(screen.getByTestId("series-title")).toHaveTextContent("Approval workflow");
    expect(screen.queryByTestId(/^series-section-(overview|tickets|talk)$/)).toBeNull();
    expect(screen.queryByTestId("series-tickets")).toBeNull();
    expect(hoisted.scrolled).not.toHaveBeenCalled();
  });

  it("SeriesPage_AnOlderTalkLink_ScrollsToTheChat", async () => {
    hoisted.search = "section=talk";
    render(<SeriesRunPage id="sr-1" />);

    await screen.findByTestId("series-chat");
    expect(hoisted.scrolled).toHaveBeenCalledTimes(1);
  });

  it("SeriesPage_Chat_PostsToTheSeriesOwnConversation", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-talk"); // that it lands on the thread: SeriesConversationSection.test.tsx, real hook

    fireEvent.change(screen.getByTestId("dialog-composer-text"), { target: { value: "The worker needs the callback URL" } });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() => expect(hoisted.dialog.send).toHaveBeenCalledWith("The worker needs the callback URL", "shop"));
  });

  it("SeriesPage_AQuestionWithChoices_DrawsThemAsButtons_TheRecommendedPrimary_AClickAnswers", async () => {
    hoisted.fetch.mockResolvedValue(asked());
    render(<SeriesRunPage id="sr-1" />);

    const merge = await screen.findByTestId("dialog-answer-Merge it");
    const look = screen.getByTestId("dialog-answer-I'll look at the PR first");
    expect(merge).toHaveClass("primary");
    expect(merge).toHaveTextContent("(recommended)");
    expect(look).not.toHaveClass("primary");
    expect(screen.getByTestId("series-question")).toHaveTextContent("Or write your own answer below.");

    fireEvent.click(merge);

    expect(hoisted.dialog.send).toHaveBeenCalledWith("Merge it", "shop");
    expect(screen.queryByTestId("series-question")).toBeNull();
    expect(screen.queryByTestId("series-questions")).toBeNull(); // 2026-11-26-5201c: no empty block left above the input
  });

  it("SeriesPage_AFailedSend_BringsTheChoicesBack", async () => {
    hoisted.fetch.mockResolvedValue(asked());
    const page = render(<SeriesRunPage id="sr-1" />);
    fireEvent.click(await screen.findByTestId("dialog-answer-Merge it"));
    expect(screen.queryByTestId("series-question")).toBeNull();

    hoisted.dialog.failure = new Error("offline") as unknown as null;
    page.rerender(<SeriesRunPage id="sr-1" />);

    expect(screen.getByTestId("series-question")).toBeInTheDocument();
    hoisted.dialog.failure = null;
  });

  // 2026-11-26-5201c: another viewer sees the question too, without buttons, and who answers it here.
  it("SeriesPage_WithoutRunsControl_ShowsTheQuestionWithoutChoiceButtons", async () => {
    hoisted.lacks = true;
    hoisted.fetch.mockResolvedValue(asked());
    render(<SeriesRunPage id="sr-1" />);

    const card = await screen.findByTestId("series-question");
    expect(card).toHaveTextContent("#50 is ready to merge.");
    expect(card).toHaveTextContent("Someone who may control runs answers it here.");
    expect(screen.queryByTestId("dialog-answer-Merge it")).toBeNull();
  });

  // 2026-11-26-5201c: a question without choices is a card too, naming its ticket, answered by writing below it.
  it("SeriesPage_AQuestionWithoutChoices_IsACardNamingItsTicket_AnsweredBelow", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({ question: "Which callback URL?", questionTicket: "#19664" }));
    render(<SeriesRunPage id="sr-1" />);

    const card = await screen.findByTestId("series-question");
    expect(card).toHaveTextContent("#19664 asks"); // 2026-11-26-b186: the asker is the card's label
    expect(card).toHaveTextContent("Which callback URL?");
    expect(card).toHaveTextContent("Answer below.");
    expect(card.querySelector("button")).toBeNull();
    expect(screen.getByTestId("dialog-composer-text")).toBeInTheDocument();
  });

  it("SeriesPage_TheQuestionBlock_SitsDirectlyAboveTheInput", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({ question: "Which callback URL?" }));
    render(<SeriesRunPage id="sr-1" />);

    const block = await screen.findByTestId("series-questions");
    expect(block).toHaveAttribute("id", "series-question");
    expect(block).toHaveTextContent("The series asks");
    const following = block.compareDocumentPosition(screen.getByTestId("dialog-composer-text"));
    expect(following & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByTestId("series-chat-scroll")).not.toContainElement(block);
  });

  // 2026-11-26-f511a: every ticket that needs a person gets a line under "Needs you", each with its one link.
  it("SeriesPage_EveryTicketThatNeedsYou_IsALineWithItsLink_AndTheRailCountsThem", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({
      question: "Which callback URL?", questionTicket: "#61",
      tickets: [
        seriesTicket({ ticketId: "61", key: "#61", kind: "question", hold: hold("question_plan", { act: { kind: "conversation", target: "sr-1" } }) }),
        seriesTicket({ ticketId: "62", key: "#62", kind: "question", hold: hold("question") }),
        seriesTicket({ ticketId: "63", key: "#63", kind: "failed", hold: hold("failed", { act: { kind: "retry", target: "run-63" } }) }),
        seriesTicket({ ticketId: "64", key: "#64", kind: "approval",
          hold: hold("approval_needed", { act: { kind: "pull_request", target: "https://git.test/server/pull/9964" } }) }),
        seriesTicket({ ticketId: "65", key: "#65", kind: "draft",
          hold: hold("pr_draft", { act: { kind: "pull_request", target: "https://git.test/server/pull/9965" } }) }),
        seriesTicket({ ticketId: "66", key: "#66", kind: "failed", state: "failed, waits for your decision", hold: null }),
        seriesTicket({ ticketId: "67", key: "#67", kind: "running", hold: hold("working") }),
      ],
    }));
    render(<SeriesRunPage id="sr-1" />);

    const needs = await screen.findByTestId("series-needs-you");
    expect(needs).toHaveTextContent("Needs you");
    const link = (id: string) => screen.getByTestId(`series-ticket-question-${id}`).querySelector("a");
    expect(link("62")).toHaveAttribute("href", "/tickets/shop/62");
    expect(link("62")).toHaveTextContent("Answer on its ticket");
    expect(link("63")).toHaveAttribute("href", "/tickets/shop/63");
    expect(link("63")).toHaveTextContent("Open ticket");
    expect(link("64")).toHaveAttribute("href", "https://git.test/server/pull/9964");
    expect(link("64")).toHaveTextContent("Open pull request");
    expect(link("65")).toHaveAttribute("href", "https://git.test/server/pull/9965");
    expect(screen.getByTestId("series-ticket-question-66")).toHaveTextContent("#66 failed, waits for your decision");
    expect(screen.queryByTestId("series-ticket-question-61")).toBeNull();
    expect(screen.queryByTestId("series-ticket-question-67")).toBeNull();
    const rail = screen.getByTestId("series-question-link");
    expect(rail).toHaveTextContent("6 need you");
    expect(rail).toHaveAttribute("href", "#series-question");
  });

  it("SeriesPage_NoSeriesQuestion_AnApprovalStillShowsAboveTheInput", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({
      tickets: [seriesTicket({ ticketId: "64", key: "#64", kind: "approval",
        hold: hold("approval_needed", { act: { kind: "pull_request", target: "https://git.test/server/pull/9964" } }) })],
    }));
    render(<SeriesRunPage id="sr-1" />);

    const block = await screen.findByTestId("series-questions");
    expect(screen.queryByTestId("series-question")).toBeNull();
    expect(block).toHaveTextContent("#64");
    const following = block.compareDocumentPosition(screen.getByTestId("dialog-composer-text"));
    expect(following & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("SeriesPage_AQuestionLink_ScrollsToTheQuestionBlock", async () => {
    hoisted.search = "section=question";
    hoisted.fetch.mockResolvedValue(seriesDetail({ question: "Which callback URL?" }));
    render(<SeriesRunPage id="sr-1" />);

    await screen.findByTestId("series-questions");
    expect(hoisted.scrolled).toHaveBeenCalledTimes(1);
    expect(hoisted.scrolled.mock.instances[0]).toBe(screen.getByTestId("series-questions"));
  });

  it("SeriesPage_AGoneConversation_StillShowsTheQuestion", async () => {
    hoisted.fetch.mockResolvedValue({ ...asked(), threadId: null });
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-question")).toHaveTextContent("#50 is ready to merge.");
    expect(screen.getByTestId("series-talk-gone")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-answer-Merge it")).toBeNull();
  });

  it("SeriesPage_ADecisionPending_TheQuestionSaysWhereToAnswer", async () => {
    hoisted.dialog.awaitingProposal = {} as unknown as null;
    hoisted.fetch.mockResolvedValue(asked());
    render(<SeriesRunPage id="sr-1" />);

    const card = await screen.findByTestId("series-question");
    expect(card).toHaveTextContent("Continue the conversation in Work it out to answer it.");
    expect(screen.queryByTestId("dialog-answer-Merge it")).toBeNull();
    hoisted.dialog.awaitingProposal = null;
  });

  it("SeriesPage_AClosedConversation_StillShowsTheQuestion_WithoutButtons", async () => {
    hoisted.fetch.mockResolvedValue({ ...asked(), conversationOpen: false });
    render(<SeriesRunPage id="sr-1" />);

    const card = await screen.findByTestId("series-question");
    expect(card).toHaveTextContent("Continue the conversation in Work it out to answer it.");
    expect(screen.queryByTestId("dialog-answer-Merge it")).toBeNull();
  });

  it("SeriesPage_AClosedConversation_IsContinuedInWorkItOut", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({ conversationOpen: false }));
    render(<SeriesRunPage id="sr-1" />);

    expect(await screen.findByTestId("series-talk-closed")).toHaveTextContent("This conversation is closed.");
    expect(hoisted.dialog.open).not.toHaveBeenCalled();
  });

  // 2026-11-26-b186: nothing fixed stands over the conversation — no "Chat" heading, no sub line; the status line is in
  // the title row.
  it("SeriesPage_NoChromeOverTheChat_TheStatusLineInTheTitleRow", async () => {
    render(<SeriesRunPage id="sr-1" />);

    const chat = await screen.findByTestId("series-chat");
    expect(chat.querySelector("h2")).toBeNull();
    expect(screen.queryByText(/The series tells you here/)).toBeNull();
    // 2026-11-27-ecf2a: the name is the rail's heading; the main column's top row is the status line.
    expect(screen.getByTestId("series-section-bar")).toContainElement(screen.getByTestId("series-title"));
    expect(screen.getByTestId("series-title").tagName).toBe("H1");
    expect(screen.getByTestId("series-status-line").parentElement).toHaveClass("sr-heading");
  });

  it("SeriesPage_ALongQuestion_FoldsItsText_TheButtonsStay", async () => {
    const text = "Shall the recipient detail wait for the server contract? ".repeat(6);
    hoisted.fetch.mockResolvedValue(seriesDetail({ question: text, questionChoices: ["Wait", "Stub it"], questionRecommended: 0 }));
    render(<SeriesRunPage id="sr-1" />);

    const more = await screen.findByTestId("dialog-question-more");
    expect(screen.getByTestId("dialog-question-text")).toHaveClass("dq-clamp");
    expect(screen.getByTestId("dialog-answer-Wait")).toBeInTheDocument();
    fireEvent.click(more);
    expect(screen.getByTestId("dialog-question-text")).not.toHaveClass("dq-clamp");
    expect(more).toHaveTextContent("Show less");
  });

  it("SeriesPage_AShortQuestion_IsNotFolded", async () => {
    hoisted.fetch.mockResolvedValue(seriesDetail({ question: "Which callback URL?" }));
    render(<SeriesRunPage id="sr-1" />);

    await screen.findByTestId("series-question");
    expect(screen.queryByTestId("dialog-question-more")).toBeNull();
    expect(screen.getByTestId("dialog-question-text")).not.toHaveClass("dq-clamp");
  });

  it("SeriesPage_UnknownSeries_SaysSo", async () => {
    hoisted.fetch.mockRejectedValue(new ApiResponseError("/api/series-runs/x", 404, "HTTP 404"));
    render(<SeriesRunPage id="x" />);

    expect(await screen.findByTestId("series-page-unread")).toHaveTextContent("There is no such series");
  });
});
