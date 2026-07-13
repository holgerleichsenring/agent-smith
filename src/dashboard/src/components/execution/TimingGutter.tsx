"use client";

// p0183: gantt-style bar that lives inline on every ExecutionNode row.
// Width encodes duration, left offset encodes start, colour encodes status.
// The total parameter is the run's full known duration in seconds — every
// node on the tree shares the same scale so parallelism is readable.

// p0259: "cancel" is a first-class visual status — a cancelled run is neither a
// success nor a failure. Per-step rows never produce it (cancel is run-level), but
// it flows through this shared type so the run status icon/rail can render it.
// p0320d: "queued" is the capacity-waiting identity — amber like "run" but static.
// p0327: "input" is the waiting-for-operator identity — the run parked on a
// question (violet, static) and resumes as the SAME run once answered.
export type NodeStatus = "ok" | "fail" | "run" | "wait" | "cancel" | "queued" | "input";

interface TimingGutterProps {
  startSeconds: number;
  durationSeconds: number;
  totalSeconds: number;
  status: NodeStatus;
}

export function TimingGutter({
  startSeconds,
  durationSeconds,
  totalSeconds,
  status,
}: TimingGutterProps) {
  const safeTotal = totalSeconds > 0 ? totalSeconds : 1;
  const leftPct = Math.max(0, Math.min(100, (startSeconds / safeTotal) * 100));
  const widthPctRaw = (durationSeconds / safeTotal) * 100;
  const widthPct = Math.max(0.8, Math.min(100 - leftPct, widthPctRaw));
  const barClass = barClassFor(status);
  return (
    <div className="relative mx-3 h-3.5 flex-1" data-testid="timing-gutter">
      <div className="absolute left-0 right-0 top-[5px] h-1 rounded bg-stone-100" />
      <div
        data-testid="timing-gutter-bar"
        className={`absolute top-[3px] h-2 rounded ${barClass}`}
        style={{ left: `${leftPct}%`, width: `${widthPct}%` }}
      />
    </div>
  );
}

function barClassFor(status: NodeStatus): string {
  switch (status) {
    case "fail":
      return "bg-rose-200";
    case "ok":
      return "bg-emerald-200";
    case "run":
      return "bg-amber-300";
    case "wait":
      return "bg-stone-200";
    case "cancel":
      return "bg-slate-200";
    case "queued":
      return "bg-amber-200";
    case "input":
      return "bg-violet-200";
  }
}
