"use client";

import type { NodeRendererProps } from "react-arborist";
import type { TrailNode } from "@/types/trail-node";
import { StepGateChips } from "./StepGateChips";

function kindBadgeClass(kind: TrailNode["kind"]): string {
  switch (kind) {
    case "run": return "bg-stone-200 text-stone-700";
    case "step": return "bg-emerald-100 text-emerald-700";
    case "skill-call": return "bg-amber-100 text-amber-700";
    case "tool-pair": return "bg-amber-50 text-amber-600";
    case "decision": return "bg-blue-100 text-blue-700";
    case "triage": return "bg-purple-100 text-purple-700";
    default: return "bg-stone-100 text-stone-600";
  }
}

function formatDuration(ms: number | null): string {
  if (ms === null) return "";
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

export function TrailNodeRow({ node, style, dragHandle, tree }: NodeRendererProps<TrailNode>) {
  const data = node.data;
  const isOpen = node.isOpen;
  const hasChildren = data.children.length > 0;
  const selected = tree.selectedIds.has(node.id);
  return (
    <div
      style={style}
      ref={dragHandle}
      className={`flex items-center gap-2 px-2 py-1 text-sm ${selected ? "bg-stone-100" : "hover:bg-stone-50"}`}
      onClick={() => node.toggle()}
      data-testid={`trail-node-${data.kind}-${node.id}`}
    >
      <span className="w-4 text-stone-400">
        {hasChildren ? (isOpen ? "▾" : "▸") : ""}
      </span>
      <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${kindBadgeClass(data.kind)}`}>
        {data.kind}
      </span>
      <span className="flex-1 truncate text-stone-800">{data.label}</span>
      <StepGateChips chips={data.gateChips} />
      <span className="text-xs text-stone-400">{formatDuration(data.durationMs)}</span>
    </div>
  );
}
