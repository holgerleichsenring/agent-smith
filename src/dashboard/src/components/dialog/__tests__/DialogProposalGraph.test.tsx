import { describe, it, expect, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { DialogProposalPanel } from "../DialogProposalPanel";
import { DialogProposalCard } from "../DialogProposalCard";
import { DialogQuestionCard } from "../DialogQuestionCard";
import type { SpecDialogGraphTicket, SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-10-14-dbcek: a proposed list of tickets — the pane shows the ticket texts in merge order with what each waits
// for, the conversation card names them, and the approval card counts them; in a ticket's own conversation no list is
// filed, so the card offers no Approve there.

const ticket = (key: string, requires: string[] = []): SpecDialogGraphTicket => ({
  key, title: `Title of ${key}`, requirement: `The scope of ${key}.`, done: [`${key} is done`], requires, repos: ["api"],
});

const list: SpecDialogProposalPush = {
  dialogId: "d-1", kind: "tickets", bug: null, spec: null, parent: null, children: [], at: "2026-10-14T10:00:00Z",
  tickets: [ticket("order-api"), ticket("order-ui", ["order-api", "ticket:41"]), ticket("order-export", ["order-api", "ticket:PROJ-7"])],
  findings: [
    { specId: "the list", problem: "missing", why: "refunds are asked for in the upload", quote: null, evidence: null },
    { specId: "order-ui", problem: "required ticket not found", why: "ticket 41 does not exist", quote: null, evidence: null },
  ],
};

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-10-14T10:01:00Z", expiresAt: null,
};

describe("A proposed list of tickets", () => {
  it("DialogProposalPanel_TicketsKind_RendersTicketTextsInMergeOrderWithWaitsFor", () => {
    render(<DialogProposalPanel proposal={list} />);

    const tickets = Array.from(screen.getByTestId("dialog-graph-tickets").children);
    expect(tickets.map((li) => li.querySelector(".font-mono")?.textContent)).toEqual(["order-api", "order-ui", "order-export"]);
    expect(screen.getByText("3 tickets, in the order they must merge")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-graph-waits-order-api")).toHaveTextContent(/^nothing to wait for$/);
    expect(screen.getByTestId("dialog-graph-waits-order-ui")).toHaveTextContent(/^waits for order-api, #41$/);
    expect(screen.getByTestId("dialog-graph-waits-order-export")).toHaveTextContent(/^waits for order-api, PROJ-7$/);
    expect(screen.getByText("The scope of order-ui.")).toBeInTheDocument();
    expect(screen.getByText("order-ui is done")).toBeInTheDocument();
    expect(within(screen.getByTestId("dialog-graph-ticket-order-ui")).getByTestId("dialog-graph-ticket-finding"))
      .toHaveTextContent("required ticket not found");
    expect(screen.getByTestId("dialog-graph-derive")).toHaveTextContent("each ticket's run derives and reviews its own specs");
  });

  it("DialogProposalPanel_TicketsKind_ListLevelFindingAboveTheTickets", () => {
    render(<DialogProposalPanel proposal={list} />);

    const finding = screen.getByTestId("dialog-graph-list-finding");
    expect(finding).toHaveTextContent("the list — missing: refunds are asked for in the upload");
    expect(finding.compareDocumentPosition(screen.getByTestId("dialog-graph-tickets")) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("DialogProposalCard_TicketsKind_StripListsTicketKeysAndTitlesUnderNTickets", () => {
    render(<DialogProposalCard proposal={list} onInspect={vi.fn()} />);

    expect(screen.getByText("3 tickets")).toBeInTheDocument();
    const strip = screen.getByTestId("dialog-card-tickets");
    expect(strip).toHaveTextContent("order-api Title of order-api · nothing to wait for");
    expect(strip).toHaveTextContent("order-ui Title of order-ui · waits for order-api, #41");
  });

  it("DialogQuestionCard_TicketsKind_SummarySaysHowManyHaveNothingToWaitFor", () => {
    render(<DialogQuestionCard question={question} proposal={list} onAnswer={vi.fn()} />);

    expect(screen.getByTestId("dialog-approval-summary")).toHaveTextContent(
      /^File these 3 tickets\? 1 has nothing to wait for; the rest wait until what they require is merged\.$/);
  });

  it("DialogQuestionCard_UnboundConversation_ApproveReadsApproveAndFile", () => {
    render(<DialogQuestionCard question={question} proposal={list} onAnswer={vi.fn()} />);

    expect(screen.getByTestId("dialog-answer-approve")).toHaveTextContent(/^Approve & file$/);
    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
  });

  it("DialogQuestionCard_TicketsKindInBoundConversation_NoApproveAndSaysWhereAListIsFiled", () => {
    const shaped = { ...question, choices: [{ label: "Make it one ticket's series", description: "the pieces need no merge between them" }] };
    render(<DialogQuestionCard question={shaped} proposal={list} onAnswer={vi.fn()} inTicket
      ticketEffect={{ summary: "Approving is refused: the proposed cut has no order its specs can run in.", approvable: true }} />);

    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-answer-Make it one ticket's series")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-approval-summary")).toHaveTextContent(
      /^A list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket\.$/);
  });
});
