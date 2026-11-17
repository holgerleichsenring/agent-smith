"use client";

import type { ToolResultEvent } from "@/types/hub-events";

interface Props {
  event: ToolResultEvent;
}

export function ToolResultRow({ event }: Props) {
  const okClass = event.ok ? "text-app-proven" : "text-app-stopped";
  return (
    <div className="flex items-center gap-2 text-xs text-app-text-soft" data-testid="tool-result-row">
      <span className="text-app-mute">←</span>
      <span className="font-mono text-app-needs">{event.tool}</span>
      <span className={okClass}>{event.ok ? "ok" : "fail"}</span>
      <span className="text-app-mute">({event.resultLength}B result)</span>
    </div>
  );
}
