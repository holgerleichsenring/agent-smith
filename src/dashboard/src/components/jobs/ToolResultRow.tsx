"use client";

import type { ToolResultEvent } from "@/types/hub-events";

interface Props {
  event: ToolResultEvent;
}

export function ToolResultRow({ event }: Props) {
  const okClass = event.ok ? "text-emerald-300" : "text-rose-300";
  return (
    <div className="flex items-center gap-2 text-xs text-stone-300" data-testid="tool-result-row">
      <span className="text-stone-500">←</span>
      <span className="font-mono text-amber-300">{event.tool}</span>
      <span className={okClass}>{event.ok ? "ok" : "fail"}</span>
      <span className="text-stone-500">({event.resultLength}B result)</span>
    </div>
  );
}
