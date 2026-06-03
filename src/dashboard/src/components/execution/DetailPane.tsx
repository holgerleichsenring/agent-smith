"use client";

import type { ExecutionNodeProps } from "./ExecutionNode";
import type { NodeStatus } from "./TimingGutter";

// p0205: the right pane of the two-pane run detail. Renders the selected rail
// node in full: breadcrumb, title + status pill, a meta line (duration + cost),
// the handler outcome message, and the node's body (sandboxes / LLM calls /
// catalog binding / event stream — already composed by useRunExecutionTree).
// Overview nodes (Architecture / Result) are rendered by the page directly, so
// this component only ever sees execution nodes.

interface DetailPaneProps {
  node: ExecutionNodeProps | null;
  parentLabel: string | null;
}

const PILL_TEXT: Record<NodeStatus, string> = {
  ok: "done",
  fail: "failed",
  run: "running",
  wait: "waiting",
};

const PILL_CLS: Record<NodeStatus, string> = {
  ok: "bg-emerald-50 text-emerald-700",
  fail: "bg-rose-50 text-rose-700",
  run: "bg-amber-50 text-amber-700",
  wait: "bg-stone-100 text-stone-600",
};

export function DetailPane({ node, parentLabel }: DetailPaneProps) {
  if (!node) {
    return (
      <div data-testid="detail-pane" className="px-7 py-6 text-sm text-stone-400">
        Select a step from the rail to inspect it.
      </div>
    );
  }
  const meta = buildMeta(node);
  return (
    <div data-testid="detail-pane" className="h-full overflow-y-auto px-7 py-5">
      <div className="font-mono text-[11.5px] text-stone-400">
        {parentLabel ? `${parentLabel} ›` : "Execution ›"}
      </div>
      <div className="flex items-center gap-3 text-[19px] font-semibold tracking-tight">
        <span data-testid="detail-pane-title">{node.label}</span>
        <span
          data-testid="detail-pane-pill"
          className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${PILL_CLS[node.status]}`}
        >
          {PILL_TEXT[node.status]}
        </span>
      </div>
      {meta.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 font-mono text-[12.5px] text-stone-500">
          {meta.map((m) => (
            <span key={m}>{m}</span>
          ))}
        </div>
      )}
      {node.message && (
        <p data-testid="detail-pane-message" className="mt-3 max-w-2xl text-sm leading-relaxed text-stone-600">
          {node.message}
        </p>
      )}
      {node.repoSummary && (
        <p className="mt-1 font-mono text-[12.5px] text-stone-500">{node.repoSummary.text}</p>
      )}
      <div className="mt-4 border-t border-stone-100 pt-4">
        {node.body ?? (
          <div className="text-sm text-stone-400">No sub-events — fully described above.</div>
        )}
      </div>
    </div>
  );
}

function buildMeta(node: ExecutionNodeProps): string[] {
  const meta: string[] = [];
  if (node.durationLabel && node.durationLabel !== "—") meta.push(`${node.durationLabel} duration`);
  if (node.costBadge) meta.push(node.costBadge);
  return meta;
}
