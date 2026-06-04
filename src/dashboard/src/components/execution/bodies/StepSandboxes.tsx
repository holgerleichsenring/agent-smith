"use client";

import { useState } from "react";
import { SandboxBox } from "@/components/jobs/SandboxBox";
import type { SandboxRepoSnapshot } from "@/hooks/useRunExecutionTree";

// p0189 / p0203: rendered inside an ExecutionNode body for any step that
// issued SandboxCommand events. Surfaces one SandboxBox per repo touched
// during the step. p0203 changes the default: each per-repo block is
// collapsed unless that repo failed (auto-expand on failure so the
// operator sees stderr without an extra click). Operators can toggle any
// block independently.

interface StepSandboxesProps {
  runId: string;
  sandboxes: SandboxRepoSnapshot[];
}

export function StepSandboxes({ runId, sandboxes }: StepSandboxesProps) {
  if (sandboxes.length === 0) return null;
  return (
    <div data-testid="step-sandboxes" className="space-y-2">
      {sandboxes.map((sb) => (
        <SandboxBlock key={sb.repo} runId={runId} snapshot={sb} />
      ))}
    </div>
  );
}

function SandboxBlock({ runId, snapshot }: { runId: string; snapshot: SandboxRepoSnapshot }) {
  const failed = snapshot.exitCode !== null && snapshot.exitCode !== 0;
  const [expanded, setExpanded] = useState<boolean>(failed);
  return (
    <div className="space-y-1">
      <ResultBadge snapshot={snapshot} />
      <SandboxBox
        runId={runId}
        repo={snapshot.repo}
        expanded={expanded}
        ignoreL3Filter
        onToggle={() => setExpanded((v) => !v)}
        finishedDurationMs={snapshot.durationMs}
      />
    </div>
  );
}

function ResultBadge({ snapshot }: { snapshot: SandboxRepoSnapshot }) {
  const summary = snapshot.commandSummary ?? snapshot.command ?? "(no command)";
  if (snapshot.exitCode === null) {
    return (
      <div
        data-testid={`step-sandbox-status-${snapshot.repo}`}
        className="font-mono dsh-label text-amber-700"
      >
        <span className="font-semibold">{snapshot.repo}</span>{" · "}
        <span>{summary}</span>{" · "}
        <span>running…</span>
      </div>
    );
  }
  // p0222: phrase the build/test outcome explicitly so "did it pass the tests?"
  // is answerable at a glance, not inferred from a raw exit code.
  // p0227: exit -1 is the JobLoop "couldn't run" sentinel (sandbox unreachable /
  // untouched repo / canceled run) — not a real failure. Render it neutral
  // ("not run") so a canceled run's plumbing probes don't shout red.
  const couldNotRun = snapshot.exitCode === -1;
  const okLabel = couldNotRun
    ? "not run"
    : snapshot.exitCode === 0
      ? "passed"
      : `failed (exit ${snapshot.exitCode})`;
  const tone = couldNotRun
    ? "text-stone-500"
    : snapshot.exitCode === 0
      ? "text-emerald-700"
      : "text-rose-700";
  const dur = snapshot.durationMs !== null
    ? ` · ${formatDurationMs(snapshot.durationMs)}`
    : "";
  return (
    <div
      data-testid={`step-sandbox-status-${snapshot.repo}`}
      className={`font-mono dsh-label ${tone}`}
    >
      <span className="font-semibold">{snapshot.repo}</span>{" · "}
      <span>{summary}</span>{" · "}
      <span>{okLabel}{dur}</span>
    </div>
  );
}

function formatDurationMs(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  const s = ms / 1000;
  if (s < 60) return `${s.toFixed(1)}s`;
  const m = Math.floor(s / 60);
  const rem = Math.round(s - m * 60);
  return rem === 0 ? `${m}m` : `${m}m${rem}s`;
}
