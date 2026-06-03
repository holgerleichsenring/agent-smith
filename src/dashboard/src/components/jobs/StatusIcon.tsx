"use client";

import type { NodeStatus } from "@/components/execution/TimingGutter";

// p0208: shared filled-circle status glyph. ok ✓ emerald · fail ✕ rose ·
// run ● amber (pulsing) · wait ○ stone. Mirrors the p0205 NodeStatus palette
// (StatusDot) so the runs list and the run-detail rail read the same. Landed
// in the shared layer — both views inherit one icon.

const GLYPH: Record<NodeStatus, string> = {
  ok: "✓",
  fail: "✕",
  run: "●",
  wait: "○",
};

function bgClass(status: NodeStatus): string {
  switch (status) {
    case "ok":
      return "bg-emerald-600";
    case "fail":
      return "bg-rose-600";
    case "run":
      return "bg-amber-500 animate-pulse";
    case "wait":
      return "bg-stone-300";
  }
}

export function StatusIcon({ status }: { status: NodeStatus }) {
  return (
    <span
      data-testid={`status-icon-${status}`}
      role="img"
      aria-label={status}
      className={`flex h-6 w-6 flex-none items-center justify-center rounded-full text-[13px] font-bold text-white ${bgClass(status)}`}
    >
      {GLYPH[status]}
    </span>
  );
}
