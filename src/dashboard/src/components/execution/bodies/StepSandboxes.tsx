"use client";

import { SandboxBox } from "@/components/jobs/SandboxBox";
import type { SandboxRepoSnapshot } from "@/hooks/useRunExecutionTree";

// p0189: rendered inside an ExecutionNode body for any step that issued
// SandboxCommand events. Surfaces one SandboxBox per repo touched during
// the step in always-expanded mode — operators stop having to expand the
// Architecture section and click a topology node to see what's happening
// in the sandbox. For the active step the L3 fanout streams live
// stdout/stderr; for finished steps the buffer is typically empty (the
// ring lives in dashboard memory, not on the server) but the command,
// exit code and duration still tell the operator what ran.

interface StepSandboxesProps {
  runId: string;
  sandboxes: SandboxRepoSnapshot[];
}

export function StepSandboxes({ runId, sandboxes }: StepSandboxesProps) {
  if (sandboxes.length === 0) return null;
  return (
    <div data-testid="step-sandboxes" className="space-y-2">
      {sandboxes.map((sb) => (
        <div key={sb.repo} className="space-y-1">
          <ResultBadge snapshot={sb} />
          <SandboxBox
            runId={runId}
            repo={sb.repo}
            expanded
            ignoreL3Filter
            onToggle={() => {
              /* always-expanded inside the step body */
            }}
          />
        </div>
      ))}
    </div>
  );
}

function ResultBadge({ snapshot }: { snapshot: SandboxRepoSnapshot }) {
  const summary = snapshot.commandSummary ?? snapshot.command ?? "(no command)";
  if (snapshot.exitCode === null) {
    return (
      <div
        data-testid={`step-sandbox-status-${snapshot.repo}`}
        className="font-mono text-[11px] text-amber-700"
      >
        <span className="font-semibold">{snapshot.repo}</span>{" · "}
        <span>{summary}</span>{" · "}
        <span>running…</span>
      </div>
    );
  }
  const okLabel = snapshot.exitCode === 0 ? "exit 0" : `exit ${snapshot.exitCode}`;
  const tone = snapshot.exitCode === 0 ? "text-emerald-700" : "text-rose-700";
  const dur = snapshot.durationMs !== null
    ? ` · ${formatDurationMs(snapshot.durationMs)}`
    : "";
  return (
    <div
      data-testid={`step-sandbox-status-${snapshot.repo}`}
      className={`font-mono text-[11px] ${tone}`}
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
