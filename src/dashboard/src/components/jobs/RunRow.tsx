"use client";

import Link from "next/link";
import type { RunSnapshot } from "@/types/hub-events";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { StatusIcon } from "./StatusIcon";
import { CancelRequestedBadge } from "./CancelRequestedBadge";
import { DeleteRunButton } from "./DeleteRunButton";
import { toNodeStatus } from "./runStatus";

// p0208: one dense single-line run row (Azure DevOps style). status icon ·
// title + meta (#id · preset · repos · agent · branch) · progress column
// (label + colored bar) · time column (ago + duration). Whole row links to
// /jobs/{runId}. title + branch render ONLY when the snapshot carries them —
// never synthesised. Empty "no repos" rows are the honest symptom of the
// p0211 producer gap, not patched over here.

interface Props {
  snapshot: RunSnapshot;
}

function reposLabel(repos: string[]): string {
  if (repos.length === 0) return "no repos";
  if (repos.length === 1) return repos[0];
  return `${repos.length} repos`;
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
  const remainder = seconds % 60;
  return `${minutes}m ${remainder.toString().padStart(2, "0")}s`;
}

function progressLabel(status: NodeStatus, stepIndex: number, totalSteps: number): string {
  if (totalSteps <= 0) return status === "run" ? "starting" : "—";
  switch (status) {
    case "run":
      return `step ${stepIndex}/${totalSteps}`;
    case "fail":
      return `failed · ${stepIndex}/${totalSteps}`;
    case "ok":
      return `done · ${totalSteps}/${totalSteps}`;
    default:
      return `${stepIndex}/${totalSteps}`;
  }
}

function labelTone(status: NodeStatus): string {
  switch (status) {
    case "fail":
      return "text-rose-600";
    case "run":
    case "queued":
      return "text-amber-600";
    case "input":
      return "text-violet-600";
    case "ok":
      return "text-emerald-700";
    default:
      return "text-stone-500";
  }
}

function fillTone(status: NodeStatus): string {
  switch (status) {
    case "fail":
      return "bg-rose-500";
    case "run":
      return "bg-amber-500";
    default:
      return "bg-emerald-500";
  }
}

export function RunRow({ snapshot }: Props) {
  const status = toNodeStatus(snapshot.status);
  const repos = reposLabel(snapshot.repos);
  const total = snapshot.totalSteps;
  const pct = total > 0
    ? Math.max(0, Math.min(100, Math.round((snapshot.stepIndex / total) * 100)))
    : 0;
  // branch is not (yet) on RunSnapshot — the p0211 producer gap. We render
  // only what the snapshot carries; branch surfaces here once the backend
  // populates it. Never synthesised.
  const branch = (snapshot as { branch?: string | null }).branch ?? null;
  const meta = [snapshot.pipeline, repos, snapshot.agentName, branch]
    .filter((v): v is string => typeof v === "string" && v.length > 0);

  return (
    <Link
      href={`/jobs/${encodeURIComponent(snapshot.runId)}`}
      data-testid={`run-row-${snapshot.runId}`}
      className="group grid grid-cols-[24px_1fr_132px_104px_auto] items-center gap-4 border-b border-stone-100 px-5 py-3.5 text-stone-900 transition last:border-b-0 hover:bg-stone-50"
    >
      <StatusIcon status={status} />

      <div className="min-w-0">
        {/* p0330: durable cancel state is visible on the row in EVERY status —
            not just while a local click is pending on some other surface. */}
        {(snapshot.ticketTitle || snapshot.cancelRequested) && (
          <div className="flex items-center gap-2">
            {snapshot.ticketTitle && (
              <div className="min-w-0 truncate dsh-h3 font-semibold text-stone-900">
                {snapshot.ticketTitle}
              </div>
            )}
            <CancelRequestedBadge
              status={snapshot.status}
              cancelRequested={snapshot.cancelRequested}
              className="flex-none"
            />
          </div>
        )}
        <div className="mt-0.5 truncate dsh-body text-stone-500">
          {snapshot.ticketId && (
            <code className="mr-1.5 rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-mono text-stone-600">
              #{snapshot.ticketId}
            </code>
          )}
          {meta.map((part, i) => (
            <span key={i}>
              {i > 0 && <span className="mx-1.5 text-stone-300">·</span>}
              {part}
            </span>
          ))}
        </div>
      </div>

      <div className="dsh-body text-stone-500" data-testid={`run-row-${snapshot.runId}-progress`}>
        {status === "queued" || status === "input" ? (
          // p0320d: a queued run has no step progress — show its FIFO place and
          // WHY it waits instead of a misleading stepIndex/totalSteps fill.
          // p0327: same for waiting_for_input — the operator is the bottleneck.
          <>
            <span className={`mb-0.5 block font-mono dsh-mono ${labelTone(status)}`}>
              {status === "input"
                ? "waiting for input"
                : snapshot.queuePosition != null ? `queued · #${snapshot.queuePosition}` : "queued"}
            </span>
            {snapshot.summary && (
              <span className="block truncate text-xs text-stone-400">{snapshot.summary}</span>
            )}
          </>
        ) : (
          <>
            <span className={`mb-1.5 block font-mono dsh-mono ${labelTone(status)}`}>
              {progressLabel(status, snapshot.stepIndex, total)}
            </span>
            <span className="block h-1 overflow-hidden rounded bg-stone-100">
              <span
                className={`block h-full rounded ${fillTone(status)}`}
                style={{ width: `${status === "fail" || status === "run" ? pct : 100}%` }}
              />
            </span>
          </>
        )}
      </div>

      <div className="text-right dsh-body text-stone-400">
        <span className="block">{relativeAgo(snapshot.startedAt)}</span>
        <span className="mt-0.5 block font-mono dsh-mono text-stone-400">
          {status === "run" ? "running" : duration(snapshot.startedAt, snapshot.finishedAt)}
        </span>
      </div>

      {/* p0345b: per-row delete is ALWAYS visible — subtle, but never hidden
          behind a hover reveal (delete works in any state; the two-click
          confirm guards against a misclick on a live run). */}
      <div data-testid={`run-row-${snapshot.runId}-actions`}>
        <DeleteRunButton runId={snapshot.runId} />
      </div>
    </Link>
  );
}
