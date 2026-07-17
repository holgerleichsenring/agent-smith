"use client";

import { use, useMemo } from "react";
import { useRouter } from "next/navigation";
import { Ban } from "lucide-react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { useRailSelection, type RailSelectable } from "@/hooks/useRailSelection";
import { RunDetailHeader } from "@/components/jobs/RunDetailHeader";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { CapacityFootprintPanel } from "@/components/jobs/CapacityFootprintPanel";
import { RunStory } from "@/components/jobs/story/RunStory";
import { NavRail, type OverviewRailItem } from "@/components/execution/NavRail";
import { DetailPane } from "@/components/execution/DetailPane";
import { ArchitectureDetail } from "@/components/execution/ArchitectureDetail";
import { AnalyzeMarkdownSection } from "@/components/execution/AnalyzeMarkdownSection";
import { PlanDetail } from "@/components/execution/PlanDetail";
import { ResultDetail } from "@/components/execution/ResultDetail";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { deriveRunRepoNames } from "@/lib/runRepoNames";

// p0205: two-pane master/detail run detail. Left: a single-line NavRail
// (Execution steps + Overview = Architecture/Result). Right: a DetailPane that
// renders the selected node in full. Selection + expansion live in the URL hash
// (useRailSelection) so deep-links and refresh survive. Replaces the p0183
// stacked ExecutionTree + collapsible sections.

const ARCH_ID = "arch";
const PLAN_ID = "plan";
const RESULT_ID = "result";
// p0247: the Analyze-codebase step's canonical display label (backend
// CommandDisplayNames[AnalyzeCode]). When that step is selected we surface
// analyze.md in its detail pane, the same artifact shown on the Architecture
// node — so the operator finds "what the agent understood" at the step too.
const ANALYZE_STEP_LABEL = "Analyze codebase";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  return <RunDetail runId={decodeURIComponent(id)} />;
}

function RunDetail({ runId }: { runId: string }) {
  const router = useRouter();
  const { connectionState, overview } = useJobsHub();
  const events = useRunEvents(runId);

  const snapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);

  const repoNames = useMemo(
    () => deriveRunRepoNames(snapshot?.repos, events),
    [snapshot, events],
  );

  const { nodes } = useRunExecutionTree(events, snapshot, runId);
  const resultStatus = mapResultStatus(snapshot?.status);
  const flat = useMemo(() => flattenNodes(nodes), [nodes]);

  const overviewItems: OverviewRailItem[] = [
    { id: ARCH_ID, label: "Architecture", status: "ok" },
    // p0258: Plan sits right after Architecture — "what it understood" → "what
    // it intends to do" — so the operator reads them in sequence.
    { id: PLAN_ID, label: "Plan", status: "ok" },
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
  // p0259: a cancelled run shows a calm, neutral banner — not the rose ✕ a crash gets.
  const cancelSummary =
    snapshot?.status === "cancelled" && snapshot?.summary ? snapshot.summary : null;
  const stepCaption = snapshot?.totalSteps ? `step ${snapshot.stepIndex}/${snapshot.totalSteps}` : null;

  return (
    // p0220: full-bleed shared content-shell (24px gutter) — every route lines
    // up on the one width/padding policy.
    <main className="content-shell">
      {/* p0227: keep the run header pinned while the execution/detail scrolls —
          a scrolling-away title reads as unprofessional. The negative margins
          cancel the content-shell padding so the sticky bar is full-bleed and
          sits flush at the top of the scroll area. */}
      <div
        data-testid="run-detail-header-bar"
        className="sticky top-0 z-20 -mx-6 -mt-6 border-b border-stone-200 bg-[var(--color-canvas)] px-6 pb-3 pt-6"
      >
        <RunDetailHeader
          pipeline={snapshot?.pipeline ?? null}
          ticketId={snapshot?.ticketId ?? null}
          ticketTitle={snapshot?.ticketTitle ?? null}
          runId={runId}
          stepCaption={stepCaption}
          agentName={snapshot?.agentName ?? null}
          repoNames={repoNames}
          connectionState={connectionState}
          status={snapshot?.status ?? null}
          cancelRequested={snapshot?.cancelRequested ?? false}
          costUsd={snapshot?.costUsd ?? null}
          reservedGiMinutes={snapshot?.reservedGiMinutes ?? null}
          onDeleted={() => router.push("/")}
        />

        {failureSummary && (
          <div
            data-testid="run-failure-summary"
            className="mt-3 flex items-start gap-3 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900"
          >
            <span aria-hidden="true" className="text-rose-600">✕</span>
            <span>{failureSummary}</span>
          </div>
        )}

        {/* p0327: the parked run's question + answer affordance, pinned with
            the header so the operator cannot miss what the run waits on. */}
        {snapshot?.status === "waiting_for_input" && snapshot.pendingQuestion && (
          <PendingQuestionCard runId={runId} question={snapshot.pendingQuestion} />
        )}

        {/* p0336: the run's capacity calculation — what it needs, whether it fits. */}
        {snapshot?.footprint && (
          <CapacityFootprintPanel
            footprint={snapshot.footprint}
            queuePosition={snapshot.queuePosition}
          />
        )}

        {cancelSummary && (
          <div
            data-testid="run-cancel-summary"
            className="mt-3 flex items-start gap-3 rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-800"
          >
            <Ban aria-hidden="true" className="mt-0.5 h-4 w-4 flex-none text-slate-500" />
            <span>{cancelSummary}</span>
          </div>
        )}
      </div>

      {/* p0344b: the run reads as a STORY — server-computed beats, the persisted
          progress ledger, and per-criterion acceptance dispositions — over the
          mature master/detail trace below, which survives untouched as
          progressive disclosure. */}
      <RunStory snapshot={snapshot} events={events} />

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
  if (props.selected === PLAN_ID) {
    return <PlanDetail runId={props.runId} />;
  }
  if (props.selected === RESULT_ID) {
    return <ResultDetail runId={props.runId} prUrl={props.prUrl} />;
  }
  const entry = props.flat.get(props.selected);
  const node = entry?.node ?? null;
  const footer = node?.label === ANALYZE_STEP_LABEL
    ? <AnalyzeMarkdownSection runId={props.runId} />
    : undefined;
  return <DetailPane node={node} parentLabel={entry?.parentLabel ?? null} footer={footer} />;
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

// p0259: a cancelled run is not a failure — keep it out of the rose failure
// banner; it gets its own neutral cancel banner instead.
function isFailureStatus(s: string | undefined): boolean {
  return !!s && s !== "running" && s !== "success" && s !== "cancelled";
}

function mapResultStatus(status: string | undefined): NodeStatus {
  if (status === "success") return "ok";
  if (status === "running") return "run";
  if (status === "cancelled") return "cancel";
  return status ? "fail" : "wait";
}
