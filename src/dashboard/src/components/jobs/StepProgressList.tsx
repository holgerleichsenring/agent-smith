"use client";

export interface StepRow {
  index: number;
  name: string;
  status: string;
  durationMs: number | null;
}

function statusDot(status: string): string {
  const s = status.toLowerCase();
  if (s === "running") return "bg-amber-500";
  if (s === "success") return "bg-emerald-500";
  if (s === "failed" || s === "error") return "bg-rose-500";
  return "bg-stone-300";
}

function formatDuration(ms: number | null): string {
  if (ms === null) return "…";
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

export function StepProgressList({ steps }: { steps: StepRow[] }) {
  if (steps.length === 0) {
    return <p className="text-sm text-stone-500" data-testid="steps-empty">Waiting for first step…</p>;
  }
  return (
    <ol className="space-y-1" data-testid="step-progress-list">
      {steps.map((step) => (
        <li key={step.index} className="flex items-center justify-between gap-3 text-sm">
          <div className="flex items-center gap-2">
            <span className={`h-2 w-2 rounded-full ${statusDot(step.status)}`} aria-hidden="true" />
            <span className="text-stone-700">
              <span className="text-stone-400">{step.index}.</span> {step.name}
            </span>
          </div>
          <span className="text-xs text-stone-400">{formatDuration(step.durationMs)}</span>
        </li>
      ))}
    </ol>
  );
}
