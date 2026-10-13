"use client";

import Link from "next/link";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketSummary } from "@/types/tickets";
import { TicketActivity } from "./TicketActivity";
import { TicketAttemptMarks } from "./TicketAttemptMarks";
import { TicketStrip } from "./TicketStrip";
import { age, attempts, money, ticketLabel } from "./ticketFormat";

// 2026-10-13-9a59p: one ticket on the list, in the mock's .trow anatomy — state dot, key and
// title over the activity line, series strip with its PR cells, attempt marks, summed cost, age,
// chevron. The whole row opens the ticket's page (2026-10-13-9a59q); Enter does the same, being a link.

/** The state dot. An ended ticket is coloured by how its last attempt ended, so a success is
 *  never painted in the stopped colour. */
export function dotClass(t: TicketSummary): string {
  switch (t.bucket) {
    case "needs_you":
      return "need";
    case "working":
      return t.status === "running" ? "run" : "queued";
    case "waiting_on_review":
      return "human";
    case "stuck":
      return "bad";
    case "done":
      return "ok";
    default:
      return outcomeDot(t.lastOutcome);
  }
}

function outcomeDot(outcome: string | null): string {
  if (outcome === "success" || outcome === "shortfall") return "ok";
  if (outcome === "failed" || outcome === "cancelled") return "bad";
  return "idle";
}

export function ticketHref(t: TicketSummary): string {
  return ticketPath(t.project, t.ticketId);
}

export function TicketRow({ ticket }: { ticket: TicketSummary }) {
  const when = age(ticket.since);
  return (
    <Link
      href={ticketHref(ticket)}
      className="trow"
      data-testid={`ticket-row-${ticket.project}-${ticket.ticketId}`}
      data-bucket={ticket.bucket}
    >
      <span className={`sd ${dotClass(ticket)}`} data-testid="ticket-dot" />
      <div className="rmain">
        <div className="rt">
          <span className="tick">{ticketLabel(ticket)}</span>
          <span className="ttl">{ticket.title ?? ticket.ticketId}</span>
        </div>
        <TicketActivity ticket={ticket} />
      </div>
      <TicketStrip strip={ticket.seriesStrip} pullRequests={ticket.pullRequests ?? []} />
      <TicketAttemptMarks marks={ticket.attempts} count={ticket.attemptCount} />
      <div className="cost hidesm">
        {money(ticket.cost)}
        <small>{attempts(ticket.attemptCount)}</small>
      </div>
      <div className="when hidesm">{when ?? ""}</div>
      <span className="chev">›</span>
    </Link>
  );
}
