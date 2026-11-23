"use client";

import { useEffect, useState } from "react";
import { ApiResponseError } from "@/lib/apiResponse";
import { fetchSeriesRun } from "@/lib/seriesRunsApi";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-23-31dcd: one series' page reads ONE model, GET /api/series-runs/{id}; a 404 is "no such series, or not
// yours to see" (the server answers both alike), anything else a failed read.

export type SeriesRunRead =
  | { kind: "loading" }
  | { kind: "notFound" }
  | { kind: "failed" }
  | { kind: "read"; detail: SeriesRunDetail };

export function useSeriesRunDetail(id: string): SeriesRunRead {
  const [read, setRead] = useState<SeriesRunRead>({ kind: "loading" });
  useEffect(() => {
    const controller = new AbortController();
    setRead({ kind: "loading" });
    fetchSeriesRun(id, controller.signal)
      .then((detail) => { if (!controller.signal.aborted) setRead({ kind: "read", detail }); })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        setRead(error instanceof ApiResponseError && error.status === 404 ? { kind: "notFound" } : { kind: "failed" });
      });
    return () => controller.abort();
  }, [id]);
  return read;
}
