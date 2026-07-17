"use client";

import { use, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunDetailSnapshot } from "@/hooks/useRunDetailSnapshot";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { useRailSelection, type RailSelectable } from "@/hooks/useRailSelection";
import { RunDetailHeader, statusSpill } from "@/components/jobs/RunDetailHeader";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { RunSideRail } from "@/components/jobs/RunSideRail";
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

  const { nodes } = useRunExecutionTree(events, snapshot, runId);
  const resultStatus = mapResultStatus(snapshot?.status);
  const flat = useMemo(() => flattenNodes(nodes), [nodes]);

  const overviewItems: OverviewRailItem[] = [
    { id: ARCH_ID, label: "Architecture", status: "ok" },
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
              <RunSideRail
                snapshot={snapshot}
                hasDialogue={!!pendingQuestion}
                onOpenDialogue={() => setDialogueOpen(true)}
                onOpenTrace={() => setTraceOpen(true)}
                traceSteps={snapshot.totalSteps}
              />
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
      >
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
          <div data-testid="trace-master-detail" className="trace-grid">
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
          <div className="b-sub">{snapshot.summary}</div>
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
          <div className="b-sub">{snapshot.summary}</div>
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

// p0259: a cancelled run is not a failure — it gets its own neutral banner.
function isFailureStatus(s: string | undefined): boolean {
  return !!s && s !== "running" && s !== "success" && s !== "cancelled"
    && s !== "queued" && s !== "waiting_for_input";
}

function mapResultStatus(status: string | undefined): NodeStatus {
  if (status === "success") return "ok";
  if (status === "running") return "run";
  if (status === "cancelled") return "cancel";
  return status ? "fail" : "wait";
}
