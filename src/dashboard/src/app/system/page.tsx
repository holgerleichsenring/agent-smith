"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemEvents } from "@/hooks/useSystemEvents";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { SystemStatusCard } from "@/components/system/SystemStatusCard";
import { TodayActivityCard } from "@/components/system/TodayActivityCard";
import { ChannelBreakdown } from "@/components/system/ChannelBreakdown";
import { PullCycleLog } from "@/components/system/PullCycleLog";
import { WebhookLog } from "@/components/system/WebhookLog";
import { CostRollupCard } from "@/components/system/CostRollupCard";

export default function SystemPage() {
  const { connectionState, overview, systemActivity } = useJobsHub();
  const events = useSystemEvents();
  // p0175-fix: Last-24h KPIs + By-source now come from the server-side
  // rollup (systemActivity) instead of being derived client-side from
  // the capped event ring buffer. CostRollupCard reads from the run
  // snapshots (broadcaster rolls up LlmCallFinished into RunSnapshot).
  // Pull/Webhook logs still derive from the local event stream — those
  // need the per-event detail and a future slice will move them to
  // server-aggregated cycles.

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
      <TodayActivityCard activity={systemActivity} />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ChannelBreakdown activity={systemActivity} />
        <CostRollupCard overview={overview} />
      </div>
      <PullCycleLog events={events} />
      <WebhookLog events={events} />
    </main>
  );
}
