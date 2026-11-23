// 2026-11-23-933da: GET /api/pocs — the PoCs the caller may see, whether they may import, and the targets. The status
// IS the feature switch: 200 = on and readable, 404 = pocs.enabled is off, 403 = the caller lacks pocs.read. It is
// asked with or without a token, so an installation without sign-in learns the switch too.

import { apiFetch, readJson, refused } from "@/lib/apiResponse";

export const POCS_PATH = "/api/pocs";

/** The states a PoC row carries; later specs write them, this one only reads them. */
export type PocState = "importing" | "refused" | "blocked" | "ready" | "failed" | "stopped" | "deleted";

export interface PocRow {
  id: number;
  name: string;
  project: string;
  target: string;
  /** The address from the target's domain pattern; null while the target names none. */
  host: string | null;
  state: string;
  ownerName: string;
  lastRunId: string | null;
}

export interface PocTarget {
  name: string;
  sourceRepo: string;
  root: string;
}

export interface PocList {
  pocs: PocRow[];
  mayImport: boolean;
  targets: PocTarget[];
}

/** What the read said: the list, the feature off, or no permission to see it. */
export type PocsRead = { status: "on"; list: PocList } | { status: "off" } | { status: "forbidden" };

export async function fetchPocs(signal?: AbortSignal): Promise<PocsRead> {
  const res = await apiFetch(POCS_PATH, { signal });
  if (res.status === 404) return { status: "off" };
  if (res.status === 403 || res.status === 401) return { status: "forbidden" };
  if (!res.ok) throw await refused(res, POCS_PATH);
  return { status: "on", list: await readJson<PocList>(res, POCS_PATH) };
}
