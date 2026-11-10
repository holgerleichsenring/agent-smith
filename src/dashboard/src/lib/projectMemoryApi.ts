// 2026-11-10-fcd7d: a project's own memories — what its conversations and runs kept on the project — newest
// first with their standing and where each came from, and the one change a person makes: deleting one.

import { apiFetch, readJson } from "@/lib/apiResponse";

export interface ProjectMemory {
  slug: string;
  type: string;
  description: string;
  body: string;
  /** holding | unconfirmed | disputed */
  standing: string;
  record: string;
  /** conversation | run */
  sourceKind: string;
  sourceId: string;
  /** The transcript index of a conversation turn, or a run's spec id. */
  sourceRef: string | null;
  author: string | null;
  updatedAt: string;
}

const base = (project: string) => `/api/projects/${encodeURIComponent(project)}/memories`;

export async function fetchProjectMemories(project: string, signal?: AbortSignal): Promise<ProjectMemory[]> {
  const path = base(project);
  return readJson<ProjectMemory[]>(await apiFetch(path, { signal }), path);
}

export async function deleteProjectMemory(project: string, slug: string): Promise<boolean> {
  const res = await apiFetch(`${base(project)}/${encodeURIComponent(slug)}`, { method: "DELETE" });
  return res.ok;
}
