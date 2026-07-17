"use client";

import type { ReactNode } from "react";
import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "./runStatus";
import { cn } from "@/lib/utils";

// p0343b (mock fidelity): the run-detail side rail — a sticky column of
// field-blocks next to the story + trace. Every block renders REAL snapshot
// data and is honestly absent otherwise: COMPUTE only when the run has a
// p0336 footprint, PROGRESS only when step counts exist. There is NO Dialogue
// button — no thread-read endpoint exists, so none is faked.

const STATE_TONE: Record<string, string> = {
  ok: "text-emerald-700",
  fail: "text-rose-600",
  run: "text-amber-600",
  queued: "text-amber-600",
  input: "text-violet-600",
  cancel: "text-stone-500",
  wait: "text-stone-500",
};

function money(usd: number): string {
  return `$${usd.toFixed(2)}`;
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

export function RunSideRail({
  snapshot,
  onJumpToPipeline,
}: {
  snapshot: RunSnapshot;
  onJumpToPipeline: () => void;
}) {
  const status = toNodeStatus(snapshot.status);
  const footprint = snapshot.footprint ?? null;

  return (
    <aside
      data-testid="run-side-rail"
      className="card-content flex flex-col gap-4 self-start p-4 lg:sticky lg:top-24"
    >
      <Block label="State">
        <span
          data-testid="side-rail-state"
          className={cn("font-semibold", STATE_TONE[status] ?? "text-stone-700")}
        >
          {snapshot.status.replaceAll("_", " ")}
        </span>
      </Block>

      {snapshot.totalSteps > 0 && (
        <Block label="Progress">
          <span data-testid="side-rail-progress" className="font-mono dsh-mono text-stone-700">
            {snapshot.stepIndex} of {snapshot.totalSteps}
          </span>
        </Block>
      )}

      {/* Honest omission: no footprint on the row → no COMPUTE block. */}
      {footprint && (
        <Block label="Compute">
          <span data-testid="side-rail-compute" className="font-mono dsh-mono text-stone-700">
            {footprint.pods.length} {footprint.pods.length === 1 ? "pod" : "pods"} · {footprint.totalMemLimit}
          </span>
        </Block>
      )}

      <Block label="Cost">
        <span data-testid="side-rail-cost" className="font-mono dsh-mono text-stone-700">
          {money(snapshot.costUsd)}
        </span>
      </Block>

      <Block label="Elapsed">
        <span data-testid="side-rail-elapsed" className="font-mono dsh-mono text-stone-700">
          {duration(snapshot.startedAt, snapshot.finishedAt)} · {snapshot.llmCalls} LLM
        </span>
      </Block>

      <button
        type="button"
        data-testid="side-rail-pipeline-jump"
        onClick={onJumpToPipeline}
        className="rounded-md border border-stone-300 bg-[var(--color-canvas)] px-3 py-1.5 text-left dsh-body font-medium text-stone-700 transition hover:bg-stone-100"
      >
        Full pipeline{snapshot.totalSteps > 0 ? ` · ${snapshot.totalSteps} steps` : ""}
      </button>
    </aside>
  );
}

function Block({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <div className="eyebrow-uppercase text-stone-400">{label}</div>
      <div className="mt-0.5 dsh-body">{children}</div>
    </div>
  );
}
