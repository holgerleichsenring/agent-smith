"use client";

import Link from "next/link";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketSummary } from "@/types/tickets";
import { TicketActivity } from "./TicketActivity";
import { TicketAttemptMarks } from "./TicketAttemptMarks";
import { TicketStrip } from "./TicketStrip";
import { ticketDot } from "./ticketDot";
import { age, attempts, isNeverRun, money, ticketLabel } from "./ticketFormat";

// 2026-10-13-9a59p: one ticket on the list, in the mock's .trow anatomy — state dot, key and
// title over the activity line, series strip with its PR cells, attempt marks, summed cost, age,
// chevron. The whole row opens the ticket's page (2026-10-13-9a59q); Enter does the same, being a link.
// 2026-10-14-dbcen: a ticket with no run on record has no strip, marks or cost — what it waits for, by
// tracker key, or "no run on record" stands in their place.

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
      <span className={`sd ${ticketDot(ticket)}`} data-testid="ticket-dot" />
      <div className="rmain">
        <div className="rt">
          <span className="tick">{ticketLabel(ticket)}</span>
          <span className="ttl">{ticket.title ?? ticket.ticketId}</span>
        </div>
        <TicketActivity ticket={ticket} />
      </div>
      {isNeverRun(ticket) ? (
        <NoRun ticket={ticket} />
      ) : (
        <>
          <TicketStrip strip={ticket.seriesStrip} pullRequests={ticket.pullRequests ?? []} />
          <TicketAttemptMarks marks={ticket.attempts} count={ticket.attemptCount} />
          <div className="cost hidesm">
            {money(ticket.cost)}
            <small>{attempts(ticket.attemptCount)}</small>
          </div>
        </>
      )}
      <div className="when hidesm">{when ?? ""}</div>
      <span className="chev">›</span>
    </Link>
  );
}

function NoRun({ ticket }: { ticket: TicketSummary }) {
  const waits = ticket.waitsFor ?? [];
  return (
    <div className="norun hidesm" data-testid="ticket-norun">
      {waits.length > 0 ? (
        <>
          waits for{" "}
          {waits.map((w, i) => (
            <span key={w.ticketId}>
              {i > 0 && ", "}
              <b>{w.key}</b>
            </span>
          ))}
        </>
      ) : (
        "no run on record"
      )}
    </div>
  );
}
