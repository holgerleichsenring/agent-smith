"use client";

import { shortRunId } from "@/lib/runId";
import Link from "next/link";
import type { RunSnapshot } from "@/types/hub-events";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { CancelRequestedBadge } from "./CancelRequestedBadge";
import { DeleteRunButton } from "./DeleteRunButton";
import { toNodeStatus } from "./runStatus";
import { RunStats, relativeAgo } from "./RunStats";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { cn } from "@/lib/utils";

// p0343c (pixel identity): one run row in the runs-list.html mock's .rrow DOM —
// status dot · ticket ref + title + activity line · story spine (only when the
// snapshot carries server-computed beats) · progress/cost/elapsed mono columns ·
// always-visible delete · chevron. Whole row links to /jobs/{runId}. Everything
// rendered is real snapshot data; fields the snapshot does not carry are
// omitted, never synthesised.

interface Props {
  snapshot: RunSnapshot;
}

// RunSnapshot status → the mock's .rrow st-* class.
const ST_CLASS: Record<NodeStatus, string> = {
  run: "st-run",
  wait: "st-run",
  queued: "st-q",
  input: "st-need",
  ok: "st-ok",
  fail: "st-bad",
  cancel: "st-q",
};

function finishedPill(status: NodeStatus): { cls: string; label: string } | null {
  switch (status) {
    case "ok":
      return { cls: "ok", label: "done" };
    case "fail":
      return { cls: "bad", label: "failed" };
    case "cancel":
      return { cls: "q", label: "cancelled" };
    default:
      return null;
  }
}

export function RunRow({ snapshot }: Props) {
  const status = toNodeStatus(snapshot.status);
  const tick = snapshot.ticketId ? `#${snapshot.ticketId}` : `#${shortRunId(snapshot.runId)}`;
  const title = snapshot.ticketTitle ?? snapshot.pipeline;
  const pill = finishedPill(status);
  const queued = status === "queued";

  return (
    <Link
      href={`/jobs/${encodeURIComponent(snapshot.runId)}`}
      data-testid={`run-row-${snapshot.runId}`}
      className={cn("rrow", ST_CLASS[status])}
    >
      <span className="sd" />

      <div className="rmain">
        <div className="rt">
          <span className="tick">{tick}</span>
          <span className="ttl">{title}</span>
          <CancelRequestedBadge
            status={snapshot.status}
            cancelRequested={snapshot.cancelRequested}
            className="flex-none"
          />
        </div>
        <ActivityLine snapshot={snapshot} status={status} />
      </div>

      {queued ? (
        <>
          {snapshot.summary ? (
            <span className="qreason hidesm" title={formatRunSummary(snapshot.summary)}>
              {formatRunSummary(snapshot.summary)}
            </span>
          ) : (
            <span className="qreason hidesm" />
          )}
          <span className="prog" data-testid={`run-row-${snapshot.runId}-progress`}>
            {snapshot.queuePosition != null ? `pos ${snapshot.queuePosition}` : "queued"}
          </span>
          <span className="cost hidesm" />
          <span className="prog hidesm">{relativeAgo(snapshot.startedAt)}</span>
        </>
      ) : (
        <RunStats
          snapshot={snapshot}
          spineFallback={pill ? <span className={cn("pill hidesm", pill.cls)}>{pill.label}</span> : undefined}
          progressTestId={`run-row-${snapshot.runId}-progress`}
        />
      )}

      {/* p0345b: per-row delete is ALWAYS visible — never hidden behind a hover
          reveal (the two-click confirm guards against a misclick on a live run). */}
      <span data-testid={`run-row-${snapshot.runId}-actions`}>
        <DeleteRunButton runId={snapshot.runId} />
      </span>
      <span className="chev">›</span>
    </Link>
  );
}

// The mock's .act line — what the run is doing NOW (running), or how it ended
// (finished). Only real snapshot fields; no line when nothing is known.
function ActivityLine({ snapshot, status }: { snapshot: RunSnapshot; status: NodeStatus }) {
  if (status === "run" && snapshot.stepName) {
    return (
      <div className="act">
        now: <b>{snapshot.stepName}</b>
        {snapshot.agentName ? <> · {snapshot.agentName}</> : null}
      </div>
    );
  }
  if ((status === "ok" || status === "fail" || status === "cancel") && snapshot.summary) {
    const summary = formatRunSummary(snapshot.summary);
    return <div className="act" title={summary}>{summary}</div>;
  }
  // 2026-08-25-39ab: absent repos read as none, never as a dereference.
  const repos = snapshot.repos ?? [];
  if (status !== "queued" && snapshot.pipeline) {
    return (
      <div className="act">
        {snapshot.pipeline}
        {repos.length > 0 ? (
          <> · {repos.length === 1 ? repos[0] : `${repos.length} repos`}</>
        ) : null}
      </div>
    );
  }
  return null;
}
