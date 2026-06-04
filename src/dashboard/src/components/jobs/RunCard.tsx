"use client";

import Link from "next/link";
import type { RunSnapshot } from "@/types/hub-events";
import { CancelRunButton } from "./CancelRunButton";

const TERMINAL_STATUSES = new Set(["success", "failed", "error"]);

interface Props {
  snapshot: RunSnapshot;
}

const STATUS_LABEL: Record<string, string> = {
  running: "running",
  success: "success",
  failed: "failed",
  error: "error",
};

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === "running") return "bg-stone-200 text-stone-800";
  if (s === "success") return "bg-emerald-100 text-emerald-800";
  if (s === "failed" || s === "error") return "bg-rose-100 text-rose-800";
  return "bg-stone-100 text-stone-600";
}

function formatElapsed(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - start) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return `${minutes}m${remainder.toString().padStart(2, "0")}s`;
}

export function RunCard({ snapshot }: Props) {
  const reposLabel = snapshot.repos.length === 0
    ? "no repos"
    : snapshot.repos.length === 1
      ? snapshot.repos[0]
      : `${snapshot.repos.length} repos`;
  return (
    <Link
      href={`/jobs/${encodeURIComponent(snapshot.runId)}`}
      className="block rounded-lg border border-stone-200 bg-white p-4 transition hover:border-stone-300 hover:shadow-sm"
      data-testid={`run-card-${snapshot.runId}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-medium text-stone-900">
            {snapshot.ticketTitle ?? snapshot.pipeline}
          </h3>
          <p className="mt-1 truncate text-xs text-stone-500">
            {snapshot.ticketId && (
              <code className="mr-1.5 rounded bg-stone-100 px-1 py-0.5 font-mono dsh-label text-stone-700">
                #{snapshot.ticketId}
              </code>
            )}
            {snapshot.ticketTitle ? `${snapshot.pipeline} · ${reposLabel}` : reposLabel}
          </p>
        </div>
        <span className={`inline-flex flex-none rounded px-2 py-0.5 text-xs font-medium ${statusClass(snapshot.status)}`}>
          {STATUS_LABEL[snapshot.status.toLowerCase()] ?? snapshot.status}
        </span>
      </div>
      <div className="mt-3 flex items-center justify-between text-xs text-stone-500">
        <span>
          {snapshot.totalSteps > 0
            ? `step ${snapshot.stepIndex}/${snapshot.totalSteps}`
            : "idle"}
        </span>
        <span className="flex items-center gap-2">
          <span>{formatElapsed(snapshot.startedAt, snapshot.finishedAt)}</span>
          {!TERMINAL_STATUSES.has(snapshot.status.toLowerCase()) && (
            <CancelRunButton runId={snapshot.runId} cancelRequested={snapshot.cancelRequested} />
          )}
        </span>
      </div>
    </Link>
  );
}
