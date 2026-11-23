"use client";

import { useState } from "react";
import { retryAttempt } from "@/lib/ticketsApi";

// 2026-11-23-f562: the Overview's Retry — the ticket page's act (retryAttempt on the newest attempt's run id, POST
// /api/runs/{id}/retry); a refusal is shown as the server wrote it and the series is read again either way.

export type SeriesRetryOutcome = { ticketId: string; requested: true } | { ticketId: string; said: string };

export interface SeriesRetry {
  busy: string | null;
  outcome: SeriesRetryOutcome | null;
  retry: (ticketId: string, runId: string) => Promise<void>;
}

export function useSeriesRetry(reload: () => void): SeriesRetry {
  const [busy, setBusy] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<SeriesRetryOutcome | null>(null);
  const retry = async (ticketId: string, runId: string) => {
    setBusy(ticketId);
    setOutcome(null);
    try {
      const answer = await retryAttempt(runId);
      setOutcome(answer.kind === "requested" ? { ticketId, requested: true } : { ticketId, said: answer.refusal.text });
    } catch (error: unknown) {
      setOutcome({ ticketId, said: error instanceof Error ? error.message : String(error) });
    } finally {
      setBusy(null);
      reload();
    }
  };
  return { busy, outcome, retry };
}
