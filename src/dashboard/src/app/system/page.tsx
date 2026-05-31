"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemEvents } from "@/hooks/useSystemEvents";
import { useSystemExecutionTree } from "@/hooks/useSystemExecutionTree";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { ExecutionTree } from "@/components/execution/ExecutionTree";
import { CollapsibleSection } from "@/components/execution/CollapsibleSection";
import { CostRollupCard } from "@/components/system/CostRollupCard";
import { TodayActivityCard } from "@/components/system/TodayActivityCard";

// p0183: single-pane System layout. Each subsystem (tracker / webhook /
// chat / config / catalog) is one row in the execution tree; the row's
// timing gutter encodes "how long ago did this last fire", the tail shows
// the newest typed event for that subsystem, and the body holds the typed
// event drawer with filter chips, sort toggle, content search.
// Cost rollup and today-activity stay as small collapsibles below for
// at-a-glance numbers operators check without drilling in.

export default function SystemPage() {
  const { connectionState, overview, systemActivity } = useJobsHub();
  const events = useSystemEvents();
  const { nodes, windowSeconds } = useSystemExecutionTree(events);

  return (
    <main className="mx-auto max-w-6xl space-y-5 p-6">
      <header className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-3xl font-medium tracking-tight">System</h1>
          <p className="text-sm text-stone-500">
            What the watch loop is doing right now — polling, webhooks, chat,
            config & skill catalog. Click any subsystem to see its typed event
            stream with filter, sort and search.
          </p>
        </div>
        <ConnectionState state={connectionState} />
      </header>

      <ExecutionTree
        heading="Subsystems"
        caption="freshness window · most-recent activity on the right"
        totalSeconds={windowSeconds}
        nodes={nodes}
        testId="system-execution-tree"
      />

      <CollapsibleSection
        testId="cost-section"
        title="Cost rollup"
        meta="24h / 7d / 30d"
      >
        <CostRollupCard overview={overview} />
      </CollapsibleSection>

      <CollapsibleSection
        testId="activity-section"
        title="Today's activity"
        meta="server-side rollup"
      >
        <TodayActivityCard activity={systemActivity} />
      </CollapsibleSection>
    </main>
  );
}
