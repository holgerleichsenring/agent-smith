"use client";

import { useEffect, useState } from "react";
import { fetchSeriesRun } from "@/lib/specDialogApi";
import type { SeriesRunView } from "@/types/series";

/**
 * 2026-11-17-0207e: the series the open conversation filed, read again whenever the conversation changes — every
 * status the series posts arrives as a message, so the message count is the panel's clock. Null while there is none.
 */
export function useSeriesRun(dialogId: string | null, version: number): SeriesRunView | null {
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
  }, [dialogId, version]);
  return series;
}
