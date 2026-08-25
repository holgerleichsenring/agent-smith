"use client";

import { use, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunDetailSnapshot } from "@/hooks/useRunDetailSnapshot";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { useRunSteps } from "@/hooks/useRunSteps";
import { useRunStepEvents } from "@/hooks/useRunStepEvents";
import { useRailSelection, type RailSelectable } from "@/hooks/useRailSelection";
import { RunDetailHeader, statusSpill } from "@/components/jobs/RunDetailHeader";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { RunSideRail } from "@/components/jobs/RunSideRail";
import { RenderBoundary } from "@/components/shell/RenderBoundary";
import { RunStory } from "@/components/jobs/story/RunStory";
import { NavRail, type OverviewRailItem } from "@/components/execution/NavRail";
import { PaneResizeHandle } from "@/components/execution/PaneResizeHandle";
import { DetailPane } from "@/components/execution/DetailPane";
import { ExecutionNode } from "@/components/execution/ExecutionNode";
import { ArchitectureDetail } from "@/components/execution/ArchitectureDetail";
import { AnalyzeMarkdownSection } from "@/components/execution/AnalyzeMarkdownSection";
import { PlanDetail } from "@/components/execution/PlanDetail";
import { ResultDetail } from "@/components/execution/ResultDetail";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { deriveRunRepoNames } from "@/lib/runRepoNames";
import { isRunLive } from "@/lib/runLiveness";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { stepIndexOf, toRailNodes } from "@/lib/runStepRail";
import { usePersistedPaneWidth } from "@/hooks/usePersistedPaneWidth";
import { useViewportWidth } from "@/hooks/useViewportWidth";
import { cn } from "@/lib/utils";
import type { RunSnapshot } from "@/types/hub-events";

// p0343c (pixel identity): the run detail IS the run-viewer.html mock — the
// calm header (h1 + spill + .ident strip), the horizontal storybar with the
// beat-switched stage (RunStory), the sticky .sidebox, and the mock's right
// drawers: "Dialogue" (only when a real pending question exists — it hosts the
// existing PendingQuestionCard in the mock drawer chrome) and "Full pipeline"
// (hosting the EXISTING NavRail+DetailPane master/detail — nothing lost). The
// mock's 6-state preview footer does NOT ship.

const ARCH_ID = "arch";
const PLAN_ID = "plan";
const RESULT_ID = "result";

// p0395: the trace drawer's persisted dimensions — the drawer's own width and
// the master/detail split. Stored per browser, applied as CSS custom properties
// so the stylesheet defaults stay the single fallback. p0395a: persisted as
// FRACTIONS (drawer/viewport, rail/drawer) so the panes scale with the window;
// the defaults below mirror the stylesheet (`min(1320px, 96vw)` drawer width).
const TRACE_DRAWER_WIDTH_KEY = "agentsmith.trace-drawer.width";
const TRACE_RAIL_WIDTH_KEY = "agentsmith.trace-drawer.rail";
const DRAWER_MIN = 560;
const DRAWER_MAX_SHARE = 0.96;
const DRAWER_DEFAULT = 1320;
const RAIL_MIN = 220;
const RAIL_MAX = 560;

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
// p0247: the Analyze-codebase step's canonical display label (backend
// CommandDisplayNames[AnalyzeCode]).
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
  const [traceOpen, setTraceOpen] = useState(false);
  const [dialogueOpen, setDialogueOpen] = useState(false);
  // p0395: the trace drawer resizes on two axes — its own width (left-edge
  // handle) and the master/detail split (handle on the rail's edge) — and both
  // survive a reload. p0395a: both persist as fractions of their basis (the
  // viewport, resp. the drawer), so resizing the window rescales both panes.
  const viewportWidth = useViewportWidth();
  const drawerMax =
    viewportWidth == null ? DRAWER_DEFAULT : Math.round(viewportWidth * DRAWER_MAX_SHARE);
  const [drawerWidth, setDrawerWidth] = usePersistedPaneWidth(
    TRACE_DRAWER_WIDTH_KEY,
    viewportWidth,
    DRAWER_MIN,
    drawerMax,
  );
  const effectiveDrawerWidth =
    viewportWidth == null ? null : drawerWidth ?? Math.min(DRAWER_DEFAULT, drawerMax);
  const [railWidth, setRailWidth] = usePersistedPaneWidth(
    TRACE_RAIL_WIDTH_KEY,
    effectiveDrawerWidth,
    RAIL_MIN,
    RAIL_MAX,
  );
  const traceGridRef = useRef<HTMLDivElement | null>(null);

  const listSnapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);
  // p0344b: join the detail row — progressLedger/acceptance (and the other
  // detail-only fields) live on GET /api/runs/{id}, not the list payload.
  const snapshot = useRunDetailSnapshot(runId, listSnapshot);

  const repoNames = useMemo(
    () => deriveRunRepoNames(snapshot?.repos, events),
    [snapshot, events],
  );

  // p0388b: the rail comes from the RunStep projection, not from folding the
  // event log — so it is complete on first paint and after a reload, no matter
  // how far the run has outgrown the client's live event window.
  const steps = useRunSteps(runId, snapshot);
  const nodes = useMemo(() => toRailNodes(steps), [steps]);
  const resultStatus = mapResultStatus(snapshot?.status);

  const overviewItems: OverviewRailItem[] = [
    { id: ARCH_ID, label: "Architecture", status: "ok" },
    { id: PLAN_ID, label: "Plan", status: "ok" },
    { id: RESULT_ID, label: "Result", status: resultStatus },
  ];
  const selectable: RailSelectable[] = [
    ...nodes.map((n) => ({ id: n.id, status: n.status })),
    ...overviewItems,
  ];
  const selection = useRailSelection(selectable);

  const pendingQuestion =
    snapshot?.status === "waiting_for_input" ? snapshot.pendingQuestion ?? null : null;

  // Escape closes whichever drawer is open (mock behavior).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setTraceOpen(false);
        setDialogueOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const spill = statusSpill(snapshot?.status ?? null);
  const phrase = spillPhrase(snapshot);

  return (
    <div className="mock-shell mock-viewer">
      <div className={cn("wrap", spill.cls)} data-testid="run-viewer-root">
        <RunDetailHeader
          pipeline={snapshot?.pipeline ?? null}
          ticketId={snapshot?.ticketId ?? null}
          ticketTitle={snapshot?.ticketTitle ?? null}
          runId={runId}
          phrase={phrase}
          agentName={snapshot?.agentName ?? null}
          repoNames={repoNames}
          connectionState={connectionState}
          status={snapshot?.status ?? null}
          cancelRequested={snapshot?.cancelRequested ?? false}
          costUsd={snapshot?.costUsd ?? null}
          reservedGiMinutes={snapshot?.reservedGiMinutes ?? null}
          onDeleted={() => router.push("/")}
        />

        <RunStory
          runId={runId}
          snapshot={snapshot}
          events={events}
          banner={
            <RunBanner snapshot={snapshot} onAnswer={() => setDialogueOpen(true)} />
          }
          sidebox={
            snapshot ? (
              // 2026-08-25-39ab: the rail reads the widest slice of the snapshot
              // (status, compute, footprint, budget, PRs), so it is the most
              // likely surface to meet a payload it does not know. Losing it must
              // not cost the pipeline the operator came to watch.
              <RenderBoundary surface="run side rail">
                <RunSideRail
                  snapshot={snapshot}
                  hasDialogue={!!pendingQuestion}
                  onOpenDialogue={() => setDialogueOpen(true)}
                  onOpenTrace={() => setTraceOpen(true)}
                  traceSteps={snapshot.totalSteps}
                />
              </RenderBoundary>
            ) : undefined
          }
        />
      </div>

      {/* ===== dialogue drawer: the run's REAL pending question ===== */}
      {pendingQuestion && (
        <>
          <div
            className={cn("drawer-bg", dialogueOpen && "open")}
            onClick={() => setDialogueOpen(false)}
            data-testid="dialogue-drawer-bg"
          />
          <aside
            className={cn("drawer", dialogueOpen && "open")}
            aria-label="Dialogue with the run"
            data-testid="dialogue-drawer"
          >
            <div className="drawer-h">
              <h3>Dialogue — this run</h3>
              <span className="badge run">1 open</span>
              <button
                type="button"
                aria-label="Close"
                onClick={() => setDialogueOpen(false)}
                data-testid="dialogue-drawer-close"
              >
                ✕
              </button>
            </div>
            <div className="chat">
              <PendingQuestionCard runId={runId} question={pendingQuestion} />
            </div>
          </aside>
        </>
      )}

      {/* ===== trace drawer: the EXISTING master/detail, in mock chrome ===== */}
      <div
        className={cn("drawer-bg", traceOpen && "open")}
        onClick={() => setTraceOpen(false)}
        data-testid="trace-drawer-bg"
      />
      <aside
        className={cn("drawer wide", traceOpen && "open")}
        aria-label="Full pipeline"
        data-testid="trace-drawer"
        style={
          drawerWidth != null
            ? ({ "--trace-drawer-w": `${drawerWidth}px` } as React.CSSProperties)
            : undefined
        }
      >
        <PaneResizeHandle
          ariaLabel="Resize the pipeline drawer"
          testId="trace-drawer-resize"
          className="drawer-edge-handle"
          onResize={(clientX) =>
            setDrawerWidth(clamp(window.innerWidth - clientX, DRAWER_MIN, drawerMax))
          }
        />
        <div className="drawer-h">
          <h3>
            Full pipeline
            {snapshot?.totalSteps ? ` — step ${snapshot.stepIndex}/${snapshot.totalSteps}` : ""}
          </h3>
          <button
            type="button"
            aria-label="Close"
            onClick={() => setTraceOpen(false)}
            data-testid="trace-drawer-close"
          >
            ✕
          </button>
        </div>
        <div className="drawer-b" style={{ padding: 0, flex: 1 }}>
          <div
            data-testid="trace-master-detail"
            className="trace-grid"
            ref={traceGridRef}
            style={
              railWidth != null
                ? ({ "--trace-rail-w": `${railWidth}px` } as React.CSSProperties)
                : undefined
            }
          >
            <NavRail nodes={nodes} overview={overviewItems} selection={selection} />
            <PaneResizeHandle
              ariaLabel="Resize the step list"
              testId="trace-split-resize"
              className="trace-split-handle"
              onResize={(clientX) => {
                const left = traceGridRef.current?.getBoundingClientRect().left ?? 0;
                setRailWidth(clamp(clientX - left, RAIL_MIN, RAIL_MAX));
              }}
            />
            <Detail
              selected={selection.selected}
              nodes={nodes}
              runId={runId}
              snapshot={snapshot}
              pipeline={snapshot?.pipeline ?? null}
              events={events}
              repoCount={repoNames.length}
              prUrl={snapshot?.prUrl ?? null}
              pullRequests={snapshot?.pullRequests ?? null}
            />
          </div>
        </div>
      </aside>
    </div>
  );
}

// The mock's .banner — only for states that genuinely need attention. The
// paused banner carries the REAL question and opens the dialogue drawer; the
// failed/cancelled banners carry the run's real summary. No fabricated actions.
function RunBanner({
  snapshot,
  onAnswer,
}: {
  snapshot: RunSnapshot | null;
  onAnswer: () => void;
}) {
  if (!snapshot) return null;

  if (snapshot.status === "waiting_for_input") {
    const q = snapshot.pendingQuestion ?? null;
    return (
      <div className="banner wait" data-testid="run-banner" data-kind="wait">
        <div className="b-ic">?</div>
        <div className="b-body">
          <div className="b-title">
            Paused — the agent needs an answer before it will continue
          </div>
          <div className="b-sub">
            Reserved compute is held; no tokens burning while it waits.
          </div>
          {q && (
            <div className="q" data-testid="run-banner-question">
              {q.text}
              <div className="qmeta">asked {new Date(q.askedAt).toLocaleTimeString()}</div>
            </div>
          )}
          {q && (
            <div className="b-actions">
              <button type="button" className="b-btn primary" onClick={onAnswer} data-testid="run-banner-answer">
                Answer &amp; resume
              </button>
            </div>
          )}
        </div>
      </div>
    );
  }

  if (isFailureStatus(snapshot.status) && snapshot.summary) {
    return (
      <div className="banner fail" data-testid="run-failure-summary" data-kind="fail">
        <div className="b-ic">✗</div>
        <div className="b-body">
          <div className="b-title">Stopped — the run did not reach a green outcome</div>
          <div className="b-sub">{formatRunSummary(snapshot.summary)}</div>
        </div>
      </div>
    );
  }

  if (snapshot.status === "cancelled" && snapshot.summary) {
    return (
      <div className="banner cancel" data-testid="run-cancel-summary" data-kind="cancel">
        <div className="b-ic">∅</div>
        <div className="b-body">
          <div className="b-title">Cancelled</div>
          <div className="b-sub">{formatRunSummary(snapshot.summary)}</div>
        </div>
      </div>
    );
  }

  return null;
}

// The spill phrase — real facts only: the current step while running, the
// waiting state while parked; nothing otherwise.
function spillPhrase(snapshot: RunSnapshot | null): string | null {
  if (!snapshot) return null;
  if (snapshot.status === "running" && snapshot.stepName) return `on ${snapshot.stepName}`;
  if (snapshot.status === "waiting_for_input") return "paused on an open question";
  if (snapshot.status === "queued") return "waiting for capacity";
  return null;
}

interface DetailProps {
  selected: string;
  nodes: ExecutionNodeProps[];
  runId: string;
  snapshot: RunSnapshot | null;
  pipeline: string | null;
  events: ReturnType<typeof useRunEvents>;
  repoCount: number;
  prUrl: string | null;
  pullRequests: RunSnapshot["pullRequests"] | null;
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
    return <ResultDetail runId={props.runId} prUrl={props.prUrl} pullRequests={props.pullRequests} />;
  }
  return (
    <StepDetail
      runId={props.runId}
      snapshot={props.snapshot}
      railNode={props.nodes.find((n) => n.id === props.selected) ?? null}
      stepIndex={stepIndexOf(props.selected)}
    />
  );
}

// p0388b: the selected step's body is ONE clamped page of that step's own
// events, fetched on selection and paged through the Seq cursor. The rail row
// supplies the identity (label, status, duration, cost) — it comes from the
// projection and is complete; the page supplies only the body, so nothing here
// depends on the client's live event window.
//
// p0388d: the page now starts at the step's NEWEST events and follows the run
// while it is live. What it is not showing is stated above the body, with the
// walk back into history as an explicit control next to the statement.
function StepDetail({
  runId,
  snapshot,
  railNode,
  stepIndex,
}: {
  runId: string;
  snapshot: RunSnapshot | null;
  railNode: ExecutionNodeProps | null;
  stepIndex: number | null;
}) {
  const page = useRunStepEvents(runId, stepIndex, isRunLive(snapshot?.status));
  // The step's page is a bounded event list — the same composer that used to
  // fold the whole run now composes exactly one step from exactly its events.
  //
  // 2026-08-25-5d7a: and it is TOLD which step, because the page starts at the
  // step's newest row. A step longer than one page keeps its StepStarted event
  // out of reach, and a composer left to infer the step from that event dropped
  // every row the page carried — the pane read "no sub-events" and walking back
  // through history changed nothing on screen.
  const { nodes: composed } = useRunExecutionTree(page.events, snapshot, runId, stepIndex);
  const body = composed[0]?.body;
  const children = composed[0]?.children ?? [];
  const node = railNode ? { ...railNode, body } : null;
  const lead = page.hasOlder ? (
    <div
      data-testid="step-events-older-notice"
      className="mb-3 flex flex-wrap items-center gap-3 rounded-md border border-stone-200 bg-stone-50 px-3 py-2 text-sm text-stone-600"
    >
      <span>Showing this step&rsquo;s newest events — older ones exist.</span>
      <button
        type="button"
        data-testid="step-events-load-older"
        className="rounded-md border border-stone-300 bg-white px-3 py-1 text-sm text-stone-600 hover:bg-stone-50"
        disabled={page.loading}
        onClick={page.loadOlder}
      >
        {page.loading ? "Loading…" : "Load older events"}
      </button>
    </div>
  ) : null;
  const footer = (
    <>
      {children.map((c) => (
        <ExecutionNode key={c.id} {...c} depth={0} />
      ))}
      {railNode?.label === ANALYZE_STEP_LABEL && <AnalyzeMarkdownSection runId={runId} />}
    </>
  );
  // p0395: a spliced phase step names its phase ONCE, in the breadcrumb — the
  // title carries the clean step name (the rail already stripped the prefix).
  const parentLabel = railNode?.phaseId ? `Phase ${railNode.phaseId}` : null;
  return <DetailPane node={node} parentLabel={parentLabel} footer={footer} lead={lead} />;
}

// p0259: a cancelled run is not a failure — it gets its own neutral banner.
function isFailureStatus(s: string | null | undefined): boolean {
  return !!s && s !== "running" && s !== "success" && s !== "cancelled"
    && s !== "queued" && s !== "waiting_for_input";
}

function mapResultStatus(status: string | null | undefined): NodeStatus {
  if (status === "success") return "ok";
  if (status === "running") return "run";
  if (status === "cancelled") return "cancel";
  return status ? "fail" : "wait";
}
