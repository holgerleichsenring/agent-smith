"use client";

import { useState } from "react";
import { refreshTicketBase, retryAttempt } from "@/lib/ticketsApi";

// 2026-11-23-f562: the Overview's Retry — the ticket page's act (retryAttempt on the newest attempt's run id, POST
// /api/runs/{id}/retry); a refusal is shown as the server wrote it and the series is read again either way.
// 2026-11-24-9dc0e: Try again on a refused refresh is drawn and answered the same way.

export type SeriesRetryOutcome = { ticketId: string; requested: true } | { ticketId: string; refreshed: true } | { ticketId: string; said: string };

export interface SeriesRetry {
  busy: string | null;
  outcome: SeriesRetryOutcome | null;
  retry: (ticketId: string, runId: string) => Promise<void>;
  refresh: (ticketId: string, project: string, url: string) => Promise<void>;
}

export function useSeriesRetry(reload: () => void): SeriesRetry {
  const [busy, setBusy] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<SeriesRetryOutcome | null>(null);
  const act = async (ticketId: string, post: () => Promise<SeriesRetryOutcome>) => {
    setBusy(ticketId);
    setOutcome(null);
    try {
      setOutcome(await post());
    } catch (error: unknown) {
      setOutcome({ ticketId, said: error instanceof Error ? error.message : String(error) });
    } finally {
      setBusy(null);
      reload();
    }
  };
  const retry = (ticketId: string, runId: string) => act(ticketId, async () => {
    const answer = await retryAttempt(runId);
    return answer.kind === "requested" ? { ticketId, requested: true } : { ticketId, said: answer.refusal.text };
  });
  const refresh = (ticketId: string, project: string, url: string) => act(ticketId, async () => {
    const answer = await refreshTicketBase(project, ticketId, url);
    return answer.kind === "started" ? { ticketId, refreshed: true } : { ticketId, said: answer.text };
  });
  return { busy, outcome, retry, refresh };
}
