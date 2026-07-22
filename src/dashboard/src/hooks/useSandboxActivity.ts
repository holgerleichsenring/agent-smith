"use client";

import { useEffect, useState } from "react";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import type { SandboxActivityRollup } from "@/types/hub-events";

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

// p0370: the coalesced sandbox-activity beat for a run (p0367 replaced the
// per-tool-call firehose with one rollup per run per interval). Transient
// liveness — not backed by the EventStore backlog, so it reads the client
// subject directly and keeps only the latest rollup for this run.
export function useSandboxActivity(runId: string | null): SandboxActivityRollup | null {
  const [rollup, setRollup] = useState<SandboxActivityRollup | null>(null);

  useEffect(() => {
    setRollup(null);
    if (!runId) return;
    const client = getJobsHubClient(HUB_URL);
    return client.sandboxActivity.add((r) => {
      if (r.runId === runId) setRollup(r);
    });
  }, [runId]);

  return rollup;
}
