"use client";

import type { ToolCallEvent, ToolResultEvent } from "@/types/hub-events";
import { MetadataTooltip } from "./ToolCallRow";

interface Props {
  call: ToolCallEvent;
  result: ToolResultEvent;
}

export function ToolPairCard({ call, result }: Props) {
  const okClass = result.ok ? "text-emerald-300" : "text-rose-300";
  return (
    <div className="rounded border border-stone-700 bg-stone-900 px-2 py-1 text-xs" data-testid="tool-pair-card">
      <div className="flex items-center gap-2 text-stone-300">
        <span className="text-stone-500">→</span>
        <span className="font-mono text-amber-300">{call.tool}</span>
        {call.summary ? (
          <span className="truncate font-mono text-stone-300" title={call.summary}>
            {call.summary}
          </span>
        ) : (
          <span className="text-stone-500">({call.argsLength}B args)</span>
        )}
        <MetadataTooltip />
      </div>
      <div className="mt-0.5 flex items-center gap-2 text-stone-400">
        <span className="text-stone-500">←</span>
        <span className={okClass}>{result.ok ? "ok" : "fail"}</span>
        <span className="text-stone-500">({result.resultLength}B result)</span>
      </div>
    </div>
  );
}
