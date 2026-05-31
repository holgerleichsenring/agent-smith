"use client";

import { use, useMemo, useState, useCallback } from "react";
import Link from "next/link";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { ExecutionTree } from "@/components/execution/ExecutionTree";
import { CollapsibleSection } from "@/components/execution/CollapsibleSection";
import { TopologyGraph } from "@/components/jobs/TopologyGraph";
import { TopologyDetail } from "@/components/jobs/TopologyDetail";
import { ResultTab } from "@/components/jobs/ResultTab";
import { EventType } from "@/types/hub-events";

// p0183: single-pane run-detail layout replacing the prior 4-tab split
// (Topology / Activity / Trail / Result). Execution tree at top tells the
// whole story (steps + sub-agents + timing); Architecture + Result hang
// below as collapsible sections for orthogonal context.

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const runId = decodeURIComponent(id);
  return <RunDetail runId={runId} />;
}

function RunDetail({ runId }: { runId: string }) {
  const { connectionState, overview } = useJobsHub();
  const events = useRunEvents(runId);

  const snapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);

  const repoNames = useMemo(() => {
    const repos = new Set<string>();
    if (snapshot?.repos) for (const r of snapshot.repos) repos.add(r);
    for (const e of events) if (e.type === EventType.SandboxCreated) repos.add(e.repo);
    return [...repos].sort();
  }, [snapshot, events]);

  const { nodes, totalSeconds } = useRunExecutionTree(events, snapshot);

  const [selectedTopologyRepo, setSelectedTopologyRepo] = useState<string | null>(null);
  const selectTopologyRepo = useCallback((repo: string) => {
    setSelectedTopologyRepo((prev) => (prev === repo ? null : repo));
  }, []);

  const isFailureStatus = (s: string | undefined): boolean =>
    !!s && s !== "running" && s !== "success";
  const failureSummary =
    isFailureStatus(snapshot?.status) && snapshot?.summary ? snapshot.summary : null;

  const stepCaption = snapshot?.totalSteps
    ? `step ${snapshot.stepIndex}/${snapshot.totalSteps}`
    : null;

  return (
    <main className="mx-auto max-w-6xl space-y-5 p-8">
      <header className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <Link href="/" className="text-xs text-stone-500 hover:underline">
            ← runs
          </Link>
          <h1 className="text-3xl font-medium tracking-tight">
            {snapshot?.pipeline ?? "run"}
          </h1>
          <div className="font-mono text-xs text-stone-400">
            {runId}
            {stepCaption && <span className="ml-2">· {stepCaption}</span>}
            {snapshot?.agentName && (
              <span className="ml-2" data-testid="run-agent-name">· agent {snapshot.agentName}</span>
            )}
          </div>
          {repoNames.length > 0 && (
            <div className="flex flex-wrap gap-1.5 pt-1">
              {repoNames.map((r) => (
                <code
                  key={r}
                  className="rounded bg-stone-100 px-1.5 py-0.5 font-mono text-[11px] text-stone-700"
                >
                  {r}
                </code>
              ))}
            </div>
          )}
        </div>
        <ConnectionState state={connectionState} />
      </header>

      {failureSummary && (
        <div
          data-testid="run-failure-summary"
          className="flex items-start gap-3 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900"
        >
          <span aria-hidden="true" className="text-rose-600">✕</span>
          <span>{failureSummary}</span>
        </div>
      )}

      <ExecutionTree
        heading="Execution"
        caption="tree · width = duration · click any row"
        totalSeconds={totalSeconds}
        nodes={nodes}
      />

      <CollapsibleSection
        testId="architecture-section"
        title="Architecture"
        meta={`${repoNames.length} repo${repoNames.length === 1 ? "" : "s"}`}
      >
        <div className="space-y-4">
          <TopologyGraph
            pipeline={snapshot?.pipeline ?? null}
            runId={runId}
            events={events}
            selected={selectedTopologyRepo}
            onSelect={selectTopologyRepo}
          />
          <TopologyDetail runId={runId} selected={selectedTopologyRepo} />
        </div>
      </CollapsibleSection>

      <CollapsibleSection
        testId="result-section"
        title="Result"
        meta={snapshot?.prUrl ? "PR available" : undefined}
      >
        <ResultTab runId={runId} prUrl={snapshot?.prUrl ?? null} />
      </CollapsibleSection>
    </main>
  );
}
