"use client";

import { useEffect, useState } from "react";
import { fetchPullRequests } from "@/lib/pullRequestsApi";
import { isOpenPullRequest } from "@/lib/prStatus";

// p0347: the live count of OPENED pull requests, from the same GET /api/pull-requests the page
// renders, so the sidebar count can never disagree with the page. Null until the first fetch
// lands, and after a failed one: no count rather than a fabricated 0.
export function useOpenPrCount(): number | null {
  const [count, setCount] = useState<number | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetchPullRequests(controller.signal)
      .then((prs) => setCount(prs.filter((p) => isOpenPullRequest(p.status)).length))
      .catch(() => {});
    return () => controller.abort();
  }, []);
  return count;
}
