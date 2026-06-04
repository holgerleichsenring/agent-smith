"use client";

import { use, useMemo } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { useRailSelection, type RailSelectable } from "@/hooks/useRailSelection";
import { RunDetailHeader } from "@/components/jobs/RunDetailHeader";
import { NavRail, type OverviewRailItem } from "@/components/execution/NavRail";
import { DetailPane } from "@/components/execution/DetailPane";
import { ArchitectureDetail } from "@/components/execution/ArchitectureDetail";
import { ResultDetail } from "@/components/execution/ResultDetail";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { EventType } from "@/types/hub-events";

// p0205: two-pane master/detail run detail. Left: a single-line NavRail
// (Execution steps + Overview = Architecture/Result). Right: a DetailPane that
// renders the selected node in full. Selection + expansion live in the URL hash
// (useRailSelection) so deep-links and refresh survive. Replaces the p0183
// stacked ExecutionTree + collapsible sections.

const ARCH_ID = "arch";
const RESULT_ID = "result";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  return <RunDetail runId={decodeURIComponent(id)} />;
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

  const { nodes } = useRunExecutionTree(events, snapshot, runId);
  const resultStatus = mapResultStatus(snapshot?.status);
  const flat = useMemo(() => flattenNodes(nodes), [nodes]);

  const overviewItems: OverviewRailItem[] = [
    { id: ARCH_ID, label: "Architecture", status: "ok" },
    { id: RESULT_ID, label: "Result", status: resultStatus },
  ];
  const selectable: RailSelectable[] = [
    ...nodes.flatMap((n) => [
      { id: n.id, status: n.status },
      ...(n.children ?? []).map((c) => ({ id: c.id, status: c.status })),
    ]),
    ...overviewItems,
  ];
  const selection = useRailSelection(selectable);

  const failureSummary =
    isFailureStatus(snapshot?.status) && snapshot?.summary ? snapshot.summary : null;
  const stepCaption = snapshot?.totalSteps ? `step ${snapshot.stepIndex}/${snapshot.totalSteps}` : null;

  return (
    // p0205-followup: full-bleed like Azure DevOps — the two-pane layout fills
    // the viewport width instead of a centered max-w-6xl column.
    <main className="w-full px-6 py-5">
      <RunDetailHeader
        pipeline={snapshot?.pipeline ?? null}
        ticketId={snapshot?.ticketId ?? null}
        ticketTitle={snapshot?.ticketTitle ?? null}
        runId={runId}
        stepCaption={stepCaption}
        agentName={snapshot?.agentName ?? null}
        repoNames={repoNames}
        connectionState={connectionState}
      />

      {failureSummary && (
        <div
          data-testid="run-failure-summary"
          className="mt-4 flex items-start gap-3 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900"
        >
          <span aria-hidden="true" className="text-rose-600">✕</span>
          <span>{failureSummary}</span>
        </div>
      )}

      <div className="mt-5 grid min-h-[calc(100vh-14rem)] grid-cols-1 overflow-hidden rounded-lg border border-stone-200 md:grid-cols-[336px_1fr]">
        <NavRail nodes={nodes} overview={overviewItems} selection={selection} />
        <Detail
          selected={selection.selected}
          flat={flat}
          runId={runId}
          pipeline={snapshot?.pipeline ?? null}
          events={events}
          repoCount={repoNames.length}
          prUrl={snapshot?.prUrl ?? null}
        />
      </div>
    </main>
  );
}

interface DetailProps {
  selected: string;
  flat: Map<string, { node: ExecutionNodeProps; parentLabel: string | null }>;
  runId: string;
  pipeline: string | null;
  events: ReturnType<typeof useRunEvents>;
  repoCount: number;
  prUrl: string | null;
}

function Detail(props: DetailProps) {
  if (props.selected === ARCH_ID) {
    return (
      <ArchitectureDetail
        runId={props.runId}
        pipeline={props.pipeline}
        events={props.events}
        repoCount={props.repoCount}
      />
    );
  }
  if (props.selected === RESULT_ID) {
    return <ResultDetail runId={props.runId} prUrl={props.prUrl} />;
  }
  const entry = props.flat.get(props.selected);
  return <DetailPane node={entry?.node ?? null} parentLabel={entry?.parentLabel ?? null} />;
}

function flattenNodes(
  nodes: ExecutionNodeProps[],
): Map<string, { node: ExecutionNodeProps; parentLabel: string | null }> {
  const map = new Map<string, { node: ExecutionNodeProps; parentLabel: string | null }>();
  for (const n of nodes) {
    map.set(n.id, { node: n, parentLabel: null });
    for (const c of n.children ?? []) map.set(c.id, { node: c, parentLabel: n.label });
  }
  return map;
}

function isFailureStatus(s: string | undefined): boolean {
  return !!s && s !== "running" && s !== "success";
}

function mapResultStatus(status: string | undefined): NodeStatus {
  if (status === "success") return "ok";
  if (status === "running") return "run";
  return status ? "fail" : "wait";
}
