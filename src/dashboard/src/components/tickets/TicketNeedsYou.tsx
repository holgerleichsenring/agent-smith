"use client";

import Link from "next/link";
import { PROJECT_PARAM, TICKET_PARAM } from "@/lib/specDialogAddress";
import { useT } from "@/hooks/useT";
import type { TicketSummary } from "@/types/tickets";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import { holdRowAct } from "./holds/holdWords";
import { holdPullRequestHref, holdSeriesHref } from "./holds/holdRoutes";
import { TicketRow, ticketHref } from "./TicketRow";

// 2026-10-13-9a59p: a ticket that waits on a person. 2026-11-23-31dcg: its row says the hold's sentence.
// 2026-11-23-849e: the hold's one act sits inline at the row's right, a small link — no block under the row; clicking the
// row opens the ticket, where a question is answered. A hand-back's reply goes into the ticket's conversation, which Work it
// out opens, resumes or binds from the ticket and project and where dialog.write is enforced.

export function TicketNeedsYou({ ticket }: { ticket: TicketSummary }) {
  return (
    <div className="rgroup" data-testid={`ticket-needs-you-${ticket.project}-${ticket.ticketId}`}>
      <TicketRow ticket={ticket} act={<TicketHoldAct ticket={ticket} />} />
    </div>
  );
}

/** The hold's one act as a small link — a hand-back's reply, a pull request on its host, a series, else the ticket. */
export function TicketHoldAct({ ticket }: { ticket: TicketSummary }) {
  const t = useT();
  if (ticket.hold?.case === "handback")
    return <Link className="btn sm" href={conversationHref(ticket)} data-testid="ticket-handback-reply">{t("hold.act.reply")}</Link>;
  if (!ticket.hold) return null;
  // 2026-11-23-d60d: a pull request that can merge or whose CI failed opens on its host.
  const pullRequest = holdPullRequestHref(ticket.hold);
  return pullRequest ? (
    <a className="btn sm" href={pullRequest} target="_blank" rel="noreferrer" data-testid="ticket-hold-act">{holdRowAct(ticket.hold, t)}</a>
  ) : (
    <Link className="btn sm" href={holdSeriesHref(ticket.hold, seriesRunPath) ?? ticketHref(ticket)} data-testid="ticket-hold-act">
      {holdRowAct(ticket.hold, t)}
    </Link>
  );
}

export function conversationHref(ticket: TicketSummary): string {
  const params = new URLSearchParams({ [TICKET_PARAM]: ticket.ticketId, [PROJECT_PARAM]: ticket.project });
  return `/spec-dialog?${params.toString()}`;
}
