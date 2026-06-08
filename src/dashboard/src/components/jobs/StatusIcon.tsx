"use client";

import { Check, X, Loader2, Circle, Ban, type LucideIcon } from "lucide-react";
import type { NodeStatus } from "@/components/execution/TimingGutter";

// p0259: lighter status glyph — a soft-tint circle with an outline lucide icon,
// replacing the saturated filled circles + bold white unicode glyphs (which read
// as heavy / "pompous"). One icon shared by the runs list and the run-detail rail
// header. p0259 also adds the dedicated "cancel" identity (Ban, slate) so a
// cancelled run never reads as a failure (✕).

const ICON: Record<NodeStatus, LucideIcon> = {
  ok: Check,
  fail: X,
  run: Loader2,
  wait: Circle,
  cancel: Ban,
};

function toneClass(status: NodeStatus): string {
  switch (status) {
    case "ok":
      return "bg-emerald-50 text-emerald-600";
    case "fail":
      return "bg-rose-50 text-rose-600";
    case "run":
      return "bg-amber-50 text-amber-600";
    case "wait":
      return "bg-stone-100 text-stone-400";
    case "cancel":
      return "bg-slate-100 text-slate-500";
  }
}

export function StatusIcon({ status }: { status: NodeStatus }) {
  const Icon = ICON[status];
  return (
    <span
      data-testid={`status-icon-${status}`}
      role="img"
      aria-label={status}
      className={`flex h-6 w-6 flex-none items-center justify-center rounded-full ${toneClass(status)}`}
    >
      <Icon
        className={`h-3.5 w-3.5 ${status === "run" ? "animate-spin" : ""}`}
        strokeWidth={2.5}
        aria-hidden="true"
      />
    </span>
  );
}
