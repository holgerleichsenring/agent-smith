// p0347: the dashboard's READ client for the pull requests agent-smith opened.
// PR outcomes are projected durably onto the run row (per repo, so multi-repo
// runs keep every PR); GET /api/pull-requests flattens them across runs and
// joins the run/ticket facts, newest-first. The list is the source for both the
// Pull Requests page and the AppRail's live open-PR count.

import type { PullRequest } from "@/types/hub-events";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export async function fetchPullRequests(signal?: AbortSignal): Promise<PullRequest[]> {
  const res = await fetch(`${API_BASE}/api/pull-requests`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as PullRequest[];
}
