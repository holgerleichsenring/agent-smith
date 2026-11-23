"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ApiResponseError } from "@/lib/apiResponse";
import { fetchSeriesRun } from "@/lib/seriesRunsApi";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-23-31dcd: one series' page reads ONE model, GET /api/series-runs/{id}; a 404 is "no such series, or not
// yours to see" (the server answers both alike), anything else a failed read. 2026-11-23-31dce: reload reads it again
// after an act, keeping the page on screen meanwhile.

export type SeriesRunRead =
  | { kind: "loading" }
  | { kind: "notFound" }
  | { kind: "failed" }
  | { kind: "read"; detail: SeriesRunDetail };

export function useSeriesRunDetail(id: string): { read: SeriesRunRead; reload: () => void } {
  const [read, setRead] = useState<SeriesRunRead>({ kind: "loading" });
  const [version, setVersion] = useState(0);
  const shown = useRef<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    if (shown.current !== id) setRead({ kind: "loading" });
    fetchSeriesRun(id, controller.signal)
      .then((detail) => {
        if (controller.signal.aborted) return;
        shown.current = id;
        setRead({ kind: "read", detail });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || shown.current === id) return; // a failed reload keeps the page it had
        setRead(error instanceof ApiResponseError && error.status === 404 ? { kind: "notFound" } : { kind: "failed" });
      });
    return () => controller.abort();
  }, [id, version]);
  const reload = useCallback(() => setVersion((v) => v + 1), []);
  return { read, reload };
}
