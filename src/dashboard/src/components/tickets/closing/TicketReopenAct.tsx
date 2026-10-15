"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { reopenTicket } from "@/lib/ticketCloseApi";
import { TICKET_CLOSED_TRACKER, type TicketSummary } from "@/types/tickets";
import { RUNS_CONTROL } from "../detail/RetryUnchanged";

// 2026-10-15-114fd: Reopen clears a close made here — only on a closed ticket a person closed by hand; a close on the
// tracker is the tracker's to undo. It reopens no pull request and no tracker ticket. A 404 (another tab or replica
// reopened first) is the server's sentence as is.

export function TicketReopenAct({ ticket, onReopened, small }: { ticket: TicketSummary; onReopened: () => void; small?: boolean }) {
  const lacks = useLacksPermission(RUNS_CONTROL, ticket.project);
  const [sending, setSending] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  if (lacks || ticket.bucket !== "closed" || ticket.kind === TICKET_CLOSED_TRACKER) return null;

  const reopen = async () => {
    setSending(true);
    try {
      const sent = await reopenTicket(ticket.project, ticket.ticketId);
      if (sent.kind === "said") setSaid(sent.text);
      else onReopened();
    } catch (thrown) {
      setSaid(thrown instanceof Error ? thrown.message : String(thrown));
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      {said && <span className="msub" role="status" data-testid={`ticket-reopen-said-${ticket.ticketId}`}>{said}</span>}
      <button type="button" className={small ? "btn sm" : "btn"} onClick={() => void reopen()} disabled={sending}
        data-testid={`ticket-reopen-${ticket.ticketId}`}>
        Reopen
      </button>
    </>
  );
}
