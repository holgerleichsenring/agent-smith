"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemEvents } from "@/hooks/useSystemEvents";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { SystemStatusCard } from "@/components/system/SystemStatusCard";
import { TodayActivityCard } from "@/components/system/TodayActivityCard";
import { ChannelBreakdown } from "@/components/system/ChannelBreakdown";
import { TriggerLog } from "@/components/system/TriggerLog";
import { CostRollupCard } from "@/components/system/CostRollupCard";
import type { RunEvent } from "@/types/hub-events";

export default function SystemPage() {
  const { connectionState } = useJobsHub();
  const events = useSystemEvents();
  // p0173d: CostRollupCard pulls from RunEvents, not SystemEvents (LLM
  // cost is per-run-step). The page can wire in a run-event subscription
  // later; for now we pass an empty array so the card renders zero-state
  // — operators can navigate to /jobs for live cost detail until the
  // run-event subscription is wired in a follow-up slice.
  const runEvents: RunEvent[] = [];

  return (
    <main className="mx-auto max-w-6xl space-y-4 p-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-medium tracking-tight text-stone-900">System</h1>
          <p className="text-sm text-stone-500">
            What the watch loop is doing right now — polling, webhooks, chat, config + skill catalog.
          </p>
        </div>
        <ConnectionState state={connectionState} />
      </header>

      <SystemStatusCard events={events} />
      <TodayActivityCard events={events} />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ChannelBreakdown events={events} />
        <CostRollupCard events={runEvents} />
      </div>
      <TriggerLog events={events} />
    </main>
  );
}
