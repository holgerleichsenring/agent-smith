"use client";

import { useCallback, useEffect, useState } from "react";
import { actOnSeries, fetchSeriesRun } from "@/lib/specDialogApi";
import type { SeriesAct, SeriesRunView } from "@/types/series";

/**
 * 2026-11-17-0207e: the series the open conversation filed, read again whenever the conversation changes. Null while
 * there is none. 2026-11-19-1b72a: the series no longer posts its table, so the message count alone is no clock — the
 * filed work's runs (`runs`, which changes on every refetch the run nudges cause) are read as one too. `act` is the
 * head's Pause / Resume; its answer is the series as it now stands.
 */
export function useSeriesRun(
  dialogId: string | null, version: number, runs?: unknown,
): { series: SeriesRunView | null; act: (act: SeriesAct) => Promise<void> } {
  const [series, setSeries] = useState<SeriesRunView | null>(null);
  useEffect(() => {
    if (!dialogId) {
      setSeries(null);
      return;
    }
    const abort = new AbortController();
    fetchSeriesRun(dialogId, abort.signal)
      .then((read) => setSeries(read ?? null))
      .catch(() => {
        // A conversation the caller may not read runs the page without a panel; the transcript says the rest.
      });
    return () => abort.abort();
  }, [dialogId, version, runs]);
  const act = useCallback(async (what: SeriesAct) => {
    if (!dialogId) return;
    const read = await actOnSeries(dialogId, what);
    if (read) setSeries(read);
  }, [dialogId]);
  return { series, act };
}
