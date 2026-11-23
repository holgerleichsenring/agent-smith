// 2026-11-23-31dcb: the series runs (runs.read) — the list, the sidebar's running count and one series.

import { getJson } from "@/lib/apiResponse";
import type { SeriesRunActiveCount, SeriesRunDetail, SeriesRunRow } from "@/types/seriesRuns";

export function fetchSeriesRuns(project?: string | null, signal?: AbortSignal): Promise<SeriesRunRow[]> {
  const query = project ? `?project=${encodeURIComponent(project)}` : "";
  return getJson<SeriesRunRow[]>(`/api/series-runs${query}`, signal);
}

export function fetchSeriesRunCount(signal?: AbortSignal): Promise<SeriesRunActiveCount> {
  return getJson<SeriesRunActiveCount>("/api/series-runs/count", signal);
}

export function fetchSeriesRun(id: string, signal?: AbortSignal): Promise<SeriesRunDetail> {
  return getJson<SeriesRunDetail>(`/api/series-runs/${encodeURIComponent(id)}`, signal);
}
