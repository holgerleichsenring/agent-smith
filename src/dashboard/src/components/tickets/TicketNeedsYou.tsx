"use client";

import Link from "next/link";
import { TrackerText } from "@/components/ui/TrackerText";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { useParkedQuestion } from "@/hooks/useParkedQuestion";
import { PROJECT_PARAM, TICKET_PARAM } from "@/lib/specDialogAddress";
import type { TicketSummary } from "@/types/tickets";
import { TicketRow, ticketHref } from "./TicketRow";
import { age } from "./ticketFormat";

// 2026-10-13-9a59p: a ticket that waits on a person, answerable from the list. A parked question
// is the run viewer's own PendingQuestionCard — the answer resumes the same attempt. A hand-back
// has no paused run to resume: its reply belongs in the ticket's conversation, which Work it out
// opens, resumes or binds from the ticket and project and where dialog.write is enforced.

export function TicketNeedsYou({ ticket }: { ticket: TicketSummary }) {
  return (
    <div className="rgroup" data-testid={`ticket-needs-you-${ticket.project}-${ticket.ticketId}`}>
      <TicketRow ticket={ticket} />
      <div className="answer">
        {ticket.kind === "handback" ? <HandbackReply ticket={ticket} /> : <QuestionAnswer ticket={ticket} />}
      </div>
    </div>
  );
}

function QuestionAnswer({ ticket }: { ticket: TicketSummary }) {
  const runId = ticket.pendingQuestion?.runId ?? ticket.newestRunId;
  const parked = useParkedQuestion(runId, ticket.pendingQuestion?.question ?? null);
  const askedIn = askedSpec(ticket);
  if (parked.state.kind === "loading") return <div className="hint-line">Loading the question…</div>;
  if (!parked.question) {
    return (
      <div className="hint-line" data-testid="ticket-question-unavailable">
        Question unavailable — <Link href={ticketHref(ticket)}>open the ticket</Link> to answer.
      </div>
    );
  }
  return (
    <>
      <PendingQuestionCard
        runId={runId}
        question={parked.question}
        answered={parked.state.kind === "answered"}
        onAnswered={parked.onAnswered}
        label={`Send & resume attempt ${ticket.attemptCount}`}
      />
      <div className="hint-line">
        {askedIn !== null && <><b>Asked in spec {askedIn}</b> · </>}
        The answer resumes attempt {ticket.attemptCount}.
      </div>
    </>
  );
}

/** The 1-based spec the question was asked in, from the strip's asked cell. */
function askedSpec(ticket: TicketSummary): number | null {
  const index = ticket.seriesStrip?.cells.findIndex((c) => c.state === "asked") ?? -1;
  return index >= 0 ? index + 1 : null;
}

export function conversationHref(ticket: TicketSummary): string {
  const params = new URLSearchParams({ [TICKET_PARAM]: ticket.ticketId, [PROJECT_PARAM]: ticket.project });
  return `/spec-dialog?${params.toString()}`;
}

function HandbackReply({ ticket }: { ticket: TicketSummary }) {
  const posted = age(ticket.handback?.at);
  return (
    <>
      <div className="q-item" data-testid="ticket-handback">
        <div className="qt">
          {ticket.handback?.text
            ? <TrackerText text={ticket.handback.text} format="markdown" />
            : "The hand-back's words were not recorded."}
        </div>
        <div className="qm">
          {posted ? `Posted on the ticket ${posted} ago. ` : ""}
          A hand-back has no paused run to resume: the reply goes into the ticket&apos;s conversation.
        </div>
      </div>
      <Link className="btn primary" href={conversationHref(ticket)} data-testid="ticket-handback-reply">
        Reply in the conversation
      </Link>
    </>
  );
}
