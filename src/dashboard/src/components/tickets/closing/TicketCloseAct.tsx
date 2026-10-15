"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import type { TicketSummary } from "@/types/tickets";
import { RUNS_CONTROL } from "../detail/RetryUnchanged";
import { platformWords, ticketLabel } from "../ticketFormat";
import { TicketCloseDialog } from "./TicketCloseDialog";

// 2026-10-15-114fd: "Close" on a list row, "Close ticket" in the page header. Hidden from a caller known to lack
// runs.control, on a done ticket (a landed ticket is done, not closed), on a live one (stop it first) and on a closed
// one, where Reopen stands instead.

const NOT_CLOSABLE = new Set(["done", "working", "needs_you", "closed"]);

export function TicketCloseAct({ ticket, onClosed, small }: { ticket: TicketSummary; onClosed: () => void; small?: boolean }) {
  const lacks = useLacksPermission(RUNS_CONTROL);
  const [open, setOpen] = useState(false);
  if (lacks || NOT_CLOSABLE.has(ticket.bucket)) return null;
  const openPullRequests = (ticket.pullRequests ?? []).filter((pr) => pr.state === "open").length;
  return (
    <>
      <button type="button" className={small ? "btn sm" : "btn"} onClick={() => setOpen(true)} data-testid={`ticket-close-${ticket.ticketId}`}>
        {small ? "Close" : "Close ticket"}
      </button>
      {open && (
        <TicketCloseDialog project={ticket.project} ticketId={ticket.ticketId} label={ticketLabel(ticket)}
          tracker={platformWords(ticket.platform)} openPullRequests={openPullRequests} onClose={() => setOpen(false)} onClosed={onClosed} />
      )}
    </>
  );
}
