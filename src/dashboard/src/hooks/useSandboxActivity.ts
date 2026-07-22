"use client";

import { useEffect, useState } from "react";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import type { SandboxActivityRollup } from "@/types/hub-events";

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

// p0370: the coalesced sandbox-activity beat for a run (p0367 replaced the
// per-tool-call firehose with one rollup per run per interval). Transient
// liveness — not backed by the EventStore backlog, so it reads the client
// subject directly. The coalescer's `commands` is a PER-WINDOW delta (it resets
// each throttle window), which reads as "only 3 commands" even though tool calls
// fire constantly. So we ACCUMULATE the deltas into a running total here — the
// beat climbs, reflecting continuous activity. `commands` on the returned rollup
// is the running total since this view opened; `lastCommand` is the latest.
export function useSandboxActivity(runId: string | null): SandboxActivityRollup | null {
  const [rollup, setRollup] = useState<SandboxActivityRollup | null>(null);

  useEffect(() => {
    setRollup(null);
    if (!runId) return;
    const client = getJobsHubClient(HUB_URL);
    return client.sandboxActivity.add((r) => {
      if (r.runId !== runId) return;
      setRollup((prev) => ({
        ...r,
        commands: (prev?.commands ?? 0) + r.commands,
      }));
    });
  }, [runId]);

  return rollup;
}
