// 2026-10-13-0b82c: whether each repository of a project is bootstrapped, read from its host by the
// server when asked. "unknown" is its own answer — the host could not say — and carries the reason.

import { apiFetch, readJson } from "@/lib/apiResponse";

export type RepoBootstrapState = "complete" | "incomplete" | "unknown";

export interface RepoBootstrap {
  repo: string;
  /** The branch read: its configured name, or "default" for the host's default branch. */
  branch: string;
  state: RepoBootstrapState;
  missing: string[];
  reason: string | null;
}

export async function fetchProjectBootstrap(project: string, signal?: AbortSignal): Promise<RepoBootstrap[]> {
  const path = `/api/projects/${encodeURIComponent(project)}/bootstrap`;
  return readJson<RepoBootstrap[]>(await apiFetch(path, { signal }), path);
}
