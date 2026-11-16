"use client";

import { useEffect, useState } from "react";
import { fetchTicketDetail } from "@/lib/ticketsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-16-6877b: the ticket a run belongs to, for the breadcrumb: its key, its page, and which of its runs this is
// ("Run 2 of 2"). Read once per run from the ticket page's own endpoint; a run without a project or ticket has none,
// and a ticket that cannot be read keeps the link without the count.

export interface RunTicket {
  label: string;
  href: string;
  number: number | null;
  of: number | null;
}

export function useRunTicket(runId: string, snapshot: RunSnapshot | null): RunTicket | null {
  const project = snapshot?.project ?? null;
  const ticketId = snapshot?.ticketId ?? null;
  const [read, setRead] = useState<{ key: string; label: string; number: number | null; of: number | null } | null>(null);
  const key = project && ticketId ? `${project}/${ticketId}/${runId}` : null;
  useEffect(() => {
    if (!project || !ticketId || !key) return;
    const ctrl = new AbortController();
    fetchTicketDetail(project, ticketId, ctrl.signal)
      .then((detail) => {
        const at = detail.attempts.find((a) => a.runId === runId);
        setRead({ key, label: detail.summary.ticketKey ?? ticketId, number: at?.number ?? null, of: at ? detail.attempts.length : null });
      })
      .catch(() => { /* the breadcrumb keeps the plain id */ });
    return () => ctrl.abort();
  }, [project, ticketId, runId, key]);
  if (!project || !ticketId || !key) return null;
  const known = read?.key === key ? read : null;
  return { label: known?.label ?? ticketId, href: ticketPath(project, ticketId), number: known?.number ?? null, of: known?.of ?? null };
}
