"use client";

import { useCallback, useEffect, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { HUB_URL } from "@/hooks/useJobsHub";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { fetchTicketDetail } from "@/lib/ticketsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { RunSnapshot } from "@/types/hub-events";
import type { TicketAttempt } from "@/types/tickets";

// 2026-11-16-6877b: the ticket a run belongs to, for the breadcrumb: its key, its page, and which of its runs this is
// ("Run 2 of 2"). Read once per run from the ticket page's own endpoint; a run without a project or ticket has none,
// and a ticket that cannot be read keeps the link without the count.
// 2026-11-17-6d9ea: and this run's attempt with whether it is the newest — How it ended offers Retry unchanged on it,
// and says why it stopped from the attempt's end kind and detail. Read again when the run's status moves.
// 2026-11-18-d6d2d: and the ticket's latest attempt, read again whenever the ticket page itself would re-read — the
// hub's TicketActivityChanged for this ticket (a new attempt announces it), a reconnect, the window regaining focus —
// and on reread(), so an older attempt links to the latest and a requested Retry can follow the new attempt.

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
  /** The ticket's latest attempt, once read; this run's own when it is the newest. */
  latest: TicketAttempt | null;
  /** Reads the ticket again now. */
  reread: () => void;
}

interface Read {
  key: string;
  ticketKey: string | null;
  label: string;
  number: number | null;
  of: number | null;
  attempt: TicketAttempt | null;
  latest: TicketAttempt | null;
}

export function useRunTicket(runId: string, snapshot: RunSnapshot | null): RunTicket | null {
  const project = snapshot?.project ?? null;
  const ticketId = snapshot?.ticketId ?? null;
  const status = snapshot?.status ?? null;
  const [read, setRead] = useState<Read | null>(null);
  const [tick, setTick] = useState(0);
  const reread = useCallback(() => setTick((n) => n + 1), []);
  const key = project && ticketId ? `${project}/${ticketId}/${runId}` : null;
  useEffect(() => {
    if (!project || !ticketId || !key) return;
    const ctrl = new AbortController();
    fetchTicketDetail(project, ticketId, ctrl.signal)
      .then((detail) => {
        const at = detail.attempts.find((a) => a.runId === runId) ?? null;
        const latest = detail.attempts.reduce<TicketAttempt | null>((top, a) => (!top || a.number > top.number ? a : top), null);
        setRead({
          key, ticketKey: detail.summary.ticketKey ?? null, label: detail.summary.ticketKey ?? ticketId,
          number: at?.number ?? null, of: at ? detail.attempts.length : null, attempt: at, latest,
        });
      })
      .catch(() => { /* the breadcrumb keeps the plain id */ });
    return () => ctrl.abort();
  }, [project, ticketId, runId, key, status, tick]);
  useTicketNudges(project, ticketId, read?.key === key ? read.ticketKey : null, reread);
  if (!project || !ticketId || !key) return null;
  const known = read?.key === key ? read : null;
  const attempt = known?.attempt ?? null;
  const latest = known?.latest ?? null;
  return {
    label: known?.label ?? ticketId, href: ticketPath(project, ticketId), project,
    number: known?.number ?? null, of: known?.of ?? null, attempt,
    newest: attempt !== null && latest !== null && attempt.number === latest.number, latest, reread,
  };
}

/** The ticket page's own re-read triggers (TicketPageStore): the ticket's nudge, a reconnect, the window's focus. */
function useTicketNudges(project: string | null, ticketId: string | null, ticketKey: string | null, reread: () => void): void {
  useEffect(() => {
    if (!project || !ticketId) return;
    const hub = getJobsHubClient(HUB_URL);
    const offTicket = hub.ticketActivityChanged.add((nudge) => {
      if (nudge.project === project && (nudge.ticketKey === ticketKey || nudge.ticketKey === ticketId)) reread();
    });
    const offConn = hub.connectionState.add((state) => { if (state === HubConnectionState.Connected) reread(); });
    let leave: (() => Promise<void>) | null = null;
    let left = false;
    hub.subscribeOverview()
      .then((cancel) => (left ? void cancel() : (leave = cancel)))
      .catch(() => { /* the connection state surfaces it */ });
    window.addEventListener("focus", reread);
    return () => {
      left = true;
      offTicket();
      offConn();
      void leave?.();
      window.removeEventListener("focus", reread);
    };
  }, [project, ticketId, ticketKey, reread]);
}
