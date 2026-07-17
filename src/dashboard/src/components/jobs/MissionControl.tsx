"use client";

import { useMemo, type ReactNode } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { ConnectionState } from "./ConnectionState";
import { RunRow } from "./RunRow";
import { mergeNewestFirst } from "./RunsList";
import { bucketRuns, deriveMetrics } from "./mission/missionBuckets";
import { MetricStrip } from "./mission/MetricStrip";
import { NeedsYouCard } from "./mission/NeedsYouCard";

// p0343: mission control — the home screen ranks tickets-worked-as-jobs by what
// needs the operator. A metric strip, then four state-ranked sections in fixed
// priority order: Needs-you (answer inline, no navigation) → Running → Queued →
// Finished. Supersedes the flat RunsList table. Running/Queued/Finished reuse
// the proven RunRow (honest step progress); only Needs-you is a new interactive
// surface. Empty sections are omitted so the screen shows only live buckets —
// except a reassuring "nothing waiting on you" when the top priority is clear.

export function MissionControl() {
  const { connectionState, overview } = useJobsHub();

  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent) : []),
    [overview],
  );
  const buckets = useMemo(() => bucketRuns(runs), [runs]);
  const metrics = useMemo(() => deriveMetrics(runs), [runs]);

  if (overview === null && connectionState !== HubConnectionState.Connected) {
    return (
      <div className="space-y-4" data-testid="mission-skeleton">
        <ConnectionState state={connectionState} />
        <div className="grid grid-cols-2 gap-px overflow-hidden rounded-md border border-stone-200 sm:grid-cols-5">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="h-16 animate-pulse bg-stone-50" />
          ))}
        </div>
      </div>
    );
  }

  if (runs.length === 0) {
    return (
      <div className="space-y-4" data-testid="mission-empty">
        <ConnectionState state={connectionState} />
        <div className="rounded-md border border-stone-200 bg-white p-10 text-center dsh-body text-stone-400">
          No runs yet. Trigger one via the CLI, a webhook, or a poller.
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8" data-testid="mission-control">
      <MetricStrip metrics={metrics} />

      <Section
        title="Needs you"
        id="needs-you"
        count={buckets.needsYou.length}
        testId="section-needs-you"
        hint="answer here — the run resumes immediately"
        alwaysShow
        emptyLine="Nothing waiting on you."
      >
        {buckets.needsYou.map((run) => (
          <NeedsYouCard key={run.runId} snapshot={run} />
        ))}
      </Section>

      <Section
        title="Running"
        id="running"
        count={buckets.running.length}
        testId="section-running"
        // p0343b: the spine hint is honest — it only shows when the running
        // runs actually carry server-computed beats (pre-beats rows don't).
        hint={buckets.running.some((run) => run.beats) ? "live · spine shows the beat" : undefined}
      >
        <RowList runs={buckets.running} />
      </Section>

      <Section title="Queued" id="queued" count={buckets.queued.length} testId="section-queued">
        <RowList runs={buckets.queued} />
      </Section>

      <Section title="Finished" id="finished" count={buckets.finished.length} testId="section-finished">
        <RowList runs={buckets.finished} />
      </Section>
    </div>
  );
}

function RowList({ runs }: { runs: Parameters<typeof RunRow>[0]["snapshot"][] }) {
  return (
    <div className="overflow-hidden rounded-md border border-stone-200 bg-white">
      {runs.map((run) => (
        <RunRow key={run.runId} snapshot={run} />
      ))}
    </div>
  );
}

function Section({
  title,
  id,
  count,
  testId,
  hint,
  alwaysShow,
  emptyLine,
  children,
}: {
  title: string;
  /** p0345b: DOM anchor for the AppRail monitor hash-links (/#needs-you …). */
  id: string;
  count: number;
  testId: string;
  hint?: string;
  alwaysShow?: boolean;
  emptyLine?: string;
  children: ReactNode;
}) {
  if (count === 0 && !alwaysShow) return null;
  return (
    <section id={id} data-testid={testId} className="scroll-mt-6 space-y-3">
      {/* p0343b mock section header: bold title + count chip, hint right-aligned. */}
      <div className="flex items-baseline gap-2.5">
        <h2 className="dsh-h3 font-semibold text-stone-900">{title}</h2>
        <span
          data-testid={`${testId}-count`}
          className={`badge-pill border dsh-label font-medium ${
            count > 0 ? "border-stone-300 bg-stone-100 text-stone-600" : "border-transparent text-stone-300"
          }`}
        >
          {count}
        </span>
        {hint && <span className="ml-auto dsh-mono text-stone-400">{hint}</span>}
      </div>
      {count === 0 ? (
        <div className="dsh-body text-stone-400">{emptyLine}</div>
      ) : (
        children
      )}
    </section>
  );
}
