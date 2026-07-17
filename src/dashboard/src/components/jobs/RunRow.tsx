"use client";

import Link from "next/link";
import type { BeatState, RunBeats, RunSnapshot } from "@/types/hub-events";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { CancelRequestedBadge } from "./CancelRequestedBadge";
import { DeleteRunButton } from "./DeleteRunButton";
import { toNodeStatus } from "./runStatus";
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

function relativeAgo(iso: string): string {
  const then = new Date(iso).getTime();
  const seconds = Math.max(0, Math.round((Date.now() - then) / 1000));
  if (seconds < 45) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

function duration(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - start) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${(minutes % 60).toString().padStart(2, "0")}m`;
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

const SPINE_ORDER: Array<keyof RunBeats> = ["ticket", "plan", "building", "verify", "outcome"];

const SPINE_CLASS: Record<BeatState, string> = {
  done: "d",
  active: "n",
  failed: "f",
  pending: "",
  skipped: "",
};

// The mini story spine — 5 dots, one per beat, ONLY from server-computed beats.
function Spine({ beats }: { beats: RunBeats }) {
  return (
    <div
      className="spine hidesm"
      title="ticket · plan · build · verify · outcome"
      data-testid="run-row-spine"
    >
      {SPINE_ORDER.map((key) => (
        <i key={key} className={SPINE_CLASS[beats[key]] || undefined} data-beat={key} data-state={beats[key]} />
      ))}
    </div>
  );
}

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
  const total = snapshot.totalSteps;
  const tick = snapshot.ticketId ? `#${snapshot.ticketId}` : `#${snapshot.runId.slice(0, 8)}`;
  const title = snapshot.ticketTitle ?? snapshot.pipeline;
  const cost = snapshot.costUsd > 0 ? `$${snapshot.costUsd.toFixed(2)}` : "";
  const prog = total > 0 ? `${snapshot.stepIndex}/${total}` : "—";
  const elapsed =
    status === "queued" ? relativeAgo(snapshot.startedAt) : duration(snapshot.startedAt, snapshot.finishedAt);
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
            <span className="qreason hidesm">{snapshot.summary}</span>
          ) : (
            <span className="qreason hidesm" />
          )}
          <span className="prog" data-testid={`run-row-${snapshot.runId}-progress`}>
            {snapshot.queuePosition != null ? `pos ${snapshot.queuePosition}` : "queued"}
          </span>
          <span className="cost hidesm" />
          <span className="prog hidesm">{elapsed}</span>
        </>
      ) : (
        <>
          {snapshot.beats ? (
            <Spine beats={snapshot.beats} />
          ) : pill ? (
            <span className={cn("pill hidesm", pill.cls)}>{pill.label}</span>
          ) : (
            <span className="spine hidesm" />
          )}
          <span className="prog hidesm" data-testid={`run-row-${snapshot.runId}-progress`}>
            {prog}
          </span>
          <span className="cost hidesm">{cost}</span>
          <span className="prog">{elapsed}</span>
        </>
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
    return <div className="act">{snapshot.summary}</div>;
  }
  if (status !== "queued" && snapshot.pipeline) {
    return (
      <div className="act">
        {snapshot.pipeline}
        {snapshot.repos.length > 0 ? (
          <> · {snapshot.repos.length === 1 ? snapshot.repos[0] : `${snapshot.repos.length} repos`}</>
        ) : null}
      </div>
    );
  }
  return null;
}
