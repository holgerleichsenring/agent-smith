// p0347: the dashboard's READ client for the pull requests agent-smith opened.
// PR outcomes are projected durably onto the run row (per repo, so multi-repo
// runs keep every PR); GET /api/pull-requests flattens them across runs and
// joins the run/ticket facts, newest-first. The list is the source for both the
// Pull Requests page and the AppRail's live open-PR count.

import type { PullRequest } from "@/types/hub-events";
import { getJson } from "@/lib/apiResponse";

export async function fetchPullRequests(signal?: AbortSignal): Promise<PullRequest[]> {
  return getJson<PullRequest[]>(`/api/pull-requests`, signal);
}
