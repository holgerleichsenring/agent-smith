"use client";

import { useState } from "react";
import { mergeSeriesTicket, type SeriesMergeResult } from "@/lib/seriesRunsApi";

// 2026-11-23-8d16: the one merge act of the series page — Merge next's rows and Your next step both start it, so a Merge
// button anywhere on the page posts the same merge, shows the same outcome or refusal and reads the series again.

export type SeriesMergeOutcomeState = { ticketId: string; result: SeriesMergeResult } | { ticketId: string; failed: string };

export interface SeriesMerge {
  busy: string | null;
  outcome: SeriesMergeOutcomeState | null;
  merge: (ticketId: string) => Promise<void>;
}

export function useSeriesMerge(seriesId: string, reload: () => void): SeriesMerge {
  const [busy, setBusy] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<SeriesMergeOutcomeState | null>(null);
  const merge = async (ticketId: string) => {
    setBusy(ticketId);
    setOutcome(null);
    try {
      setOutcome({ ticketId, result: await mergeSeriesTicket(seriesId, ticketId) });
    } catch (error: unknown) {
      setOutcome({ ticketId, failed: error instanceof Error ? error.message : String(error) });
    } finally {
      setBusy(null);
      reload();
    }
  };
  return { busy, outcome, merge };
}
