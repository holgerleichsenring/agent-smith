// 2026-11-23-31dcb: the series runs (runs.read) — the list, the sidebar's running count and one series.
// 2026-11-23-31dce: and the merge of its next ticket (runs.control). A 404, 409 or 500 in plain text is a sentence the server
// wrote (not next, a held lease, a run that ended first) and is shown as is; any other non-2xx throws.
// 2026-11-23-31dcj: and what changed in its tickets' spec sets, read only when that part of the page opens.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type { SeriesMergeAnswer, SeriesRunActiveCount, SeriesRunChanges, SeriesRunDetail, SeriesRunRow } from "@/types/seriesRuns";

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

export function fetchSeriesRunChanges(id: string, signal?: AbortSignal): Promise<SeriesRunChanges> {
  return getJson<SeriesRunChanges>(`/api/series-runs/${encodeURIComponent(id)}/changes`, signal);
}

export type SeriesMergeResult = { kind: "merged"; answer: SeriesMergeAnswer } | { kind: "said"; text: string };

const SENTENCE_STATUSES = new Set([404, 409, 500]);

export async function mergeSeriesTicket(id: string, ticketId: string): Promise<SeriesMergeResult> {
  const path = `/api/series-runs/${encodeURIComponent(id)}/tickets/${encodeURIComponent(ticketId)}/merge`;
  const res = await apiFetch(path, { method: "POST" });
  if (res.ok) return { kind: "merged", answer: (await res.json()) as SeriesMergeAnswer };
  const text = SENTENCE_STATUSES.has(res.status) && (res.headers.get("content-type") ?? "").startsWith("text/plain")
    ? (await res.text()).trim() : "";
  if (text) return { kind: "said", text };
  throw await refused(res, path);
}
