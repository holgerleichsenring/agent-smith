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
import { cn } from "@/lib/utils";

// p0343: mission control — the home screen ranks tickets-worked-as-jobs by what
// needs the operator: Needs-you (answer inline, no navigation) → Running →
// Queued → Finished. Empty sections are omitted so the screen shows only live
// buckets — except a reassuring "nothing waiting on you" when the top priority
// is clear.
// p0343c (pixel identity): emits the runs-list.html DOM verbatim — .health
// strip, .section-head slim rules with .cnt pills and .sh-sub hints, .need
// cards, .rows of .rrow rows.

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
        <div className="health">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="metric h-16 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (runs.length === 0) {
    return (
      <div className="space-y-4" data-testid="mission-empty">
        <ConnectionState state={connectionState} />
        <div className="rows">
          <div className="rrow" style={{ cursor: "default", justifyContent: "center", display: "flex" }}>
            No runs yet. Trigger one via the CLI, a webhook, or a poller.
          </div>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="mission-control">
      <MetricStrip metrics={metrics} />

      <Section
        title="Needs you"
        id="needs-you"
        count={buckets.needsYou.length}
        amber={buckets.needsYou.length > 0}
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

      <Section
        title="Queued"
        id="queued"
        count={buckets.queued.length}
        testId="section-queued"
        hint="admission is capacity-checked — no run starts it can’t finish"
      >
        <RowList runs={buckets.queued} />
      </Section>

      <Section title="Finished" id="finished" count={buckets.finished.length} testId="section-finished">
        <RowList runs={buckets.finished} />
      </Section>

      <footer>
        Row click opens the run’s story view · “Needs you” answers resume the run without leaving
        this page.
      </footer>
    </div>
  );
}

function RowList({ runs }: { runs: Parameters<typeof RunRow>[0]["snapshot"][] }) {
  return (
    <div className="rows">
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
  amber,
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
  /** The mock's .cnt.amber attention pill (Needs-you > 0). */
  amber?: boolean;
  testId: string;
  hint?: string;
  alwaysShow?: boolean;
  emptyLine?: string;
  children: ReactNode;
}) {
  if (count === 0 && !alwaysShow) return null;
  return (
    <section id={id} data-testid={testId} className="scroll-mt-6">
      <div className="section-head">
        <h2>{title}</h2>
        <span data-testid={`${testId}-count`} className={cn("cnt", amber && "amber")}>
          {count}
        </span>
        {hint && <span className="sh-sub">{hint}</span>}
      </div>
      <div style={{ height: 14 }} />
      {count === 0 ? <div className="msub">{emptyLine}</div> : children}
    </section>
  );
}
