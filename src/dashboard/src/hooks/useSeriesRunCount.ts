"use client";

import { useEffect, useState } from "react";
import { fetchSeriesRunCount } from "@/lib/seriesRunsApi";

// 2026-11-23-31dcc: how many series run right now, from GET /api/series-runs/count — the server counts exactly the
// running series the list shows, so the item and the page cannot disagree. Read again on every page change, so the
// number is fresh after a series starts or ends without a timer. Null until the first read lands, and after a failed
// one: no count rather than a made-up 0.
export function useSeriesRunCount(pathname: string): number | null {
  const [count, setCount] = useState<number | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetchSeriesRunCount(controller.signal)
      .then((answer) => setCount(answer.running))
      .catch(() => {});
    return () => controller.abort();
  }, [pathname]);
  return count;
}
