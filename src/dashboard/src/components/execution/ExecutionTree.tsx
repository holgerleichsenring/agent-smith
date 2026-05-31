"use client";

import { ExecutionNode, type ExecutionNodeProps } from "./ExecutionNode";

interface ExecutionTreeProps {
  nodes: ExecutionNodeProps[];
  /** Used by the axis header — "0s … total/2 … total". */
  totalSeconds: number;
  /** Optional caption above the tree, e.g. "tree · width = duration". */
  caption?: string;
  /** Section title above the tree. */
  heading: string;
  testId?: string;
}

export function ExecutionTree({
  nodes,
  totalSeconds,
  caption,
  heading,
  testId = "execution-tree",
}: ExecutionTreeProps) {
  return (
    <section data-testid={testId} className="mb-6">
      <header className="mb-2.5 flex items-center gap-2.5">
        <h2 className="text-xs font-semibold uppercase tracking-wider text-stone-400">
          {heading}
        </h2>
        <span className="h-px flex-1 bg-stone-200" />
        {caption && <span className="text-xs text-stone-400">{caption}</span>}
      </header>
      <div className="ml-[340px] flex justify-between pb-1.5 font-mono text-[10px] text-stone-400">
        <span>0s</span>
        <span>~{formatSeconds(totalSeconds / 2)}</span>
        <span>{formatSeconds(totalSeconds)}</span>
      </div>
      <div className="overflow-hidden rounded-lg border border-stone-200 bg-white">
        {nodes.length === 0 ? (
          <div className="px-3.5 py-6 text-center text-sm text-stone-400">
            Waiting for first step…
          </div>
        ) : (
          nodes.map((n) => <ExecutionNode key={n.id} {...n} />)
        )}
      </div>
    </section>
  );
}

function formatSeconds(s: number): string {
  if (!isFinite(s) || s <= 0) return "—";
  if (s < 1) return `${Math.round(s * 1000)}ms`;
  if (s < 60) return `${s.toFixed(1)}s`;
  const m = Math.floor(s / 60);
  const rem = Math.round(s - m * 60);
  return rem === 0 ? `${m}m` : `${m}m${rem}s`;
}
