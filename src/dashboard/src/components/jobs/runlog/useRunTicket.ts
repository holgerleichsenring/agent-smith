"use client";

import { useEffect, useState } from "react";
import { fetchTicketDetail } from "@/lib/ticketsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { RunSnapshot } from "@/types/hub-events";
import type { TicketAttempt } from "@/types/tickets";

// 2026-11-16-6877b: the ticket a run belongs to, for the breadcrumb: its key, its page, and which of its runs this is
// ("Run 2 of 2"). Read once per run from the ticket page's own endpoint; a run without a project or ticket has none,
// and a ticket that cannot be read keeps the link without the count.
// 2026-11-17-6d9ea: and this run's attempt with whether it is the newest — How it ended offers Retry unchanged on it,
// and says why it stopped from the attempt's end kind and detail. Read again when the run's status moves.

export interface RunTicket {
  label: string;
  href: string;
  project: string;
  number: number | null;
  of: number | null;
  /** This run's attempt at the ticket, once read. */
  attempt: TicketAttempt | null;
  /** Whether no later attempt exists — only the newest is retried. */
  newest: boolean;
}

interface Read {
  key: string;
  label: string;
  number: number | null;
  of: number | null;
  attempt: TicketAttempt | null;
  newest: boolean;
}

export function useRunTicket(runId: string, snapshot: RunSnapshot | null): RunTicket | null {
  const project = snapshot?.project ?? null;
  const ticketId = snapshot?.ticketId ?? null;
  const status = snapshot?.status ?? null;
  const [read, setRead] = useState<Read | null>(null);
  const key = project && ticketId ? `${project}/${ticketId}/${runId}` : null;
  useEffect(() => {
    if (!project || !ticketId || !key) return;
    const ctrl = new AbortController();
    fetchTicketDetail(project, ticketId, ctrl.signal)
      .then((detail) => {
        const at = detail.attempts.find((a) => a.runId === runId) ?? null;
        const newestNumber = Math.max(0, ...detail.attempts.map((a) => a.number));
        setRead({
          key, label: detail.summary.ticketKey ?? ticketId, number: at?.number ?? null, of: at ? detail.attempts.length : null,
          attempt: at, newest: at !== null && at.number === newestNumber,
        });
      })
      .catch(() => { /* the breadcrumb keeps the plain id */ });
    return () => ctrl.abort();
  }, [project, ticketId, runId, key, status]);
  if (!project || !ticketId || !key) return null;
  const known = read?.key === key ? read : null;
  return {
    label: known?.label ?? ticketId, href: ticketPath(project, ticketId), project,
    number: known?.number ?? null, of: known?.of ?? null, attempt: known?.attempt ?? null, newest: known?.newest ?? false,
  };
}
